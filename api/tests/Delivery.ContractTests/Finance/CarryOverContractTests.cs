using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Delivery.Application.Common.Interfaces;
using Delivery.ContractTests.Infrastructure;
using Delivery.ContractTests.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.ContractTests.Finance;

/// <summary>
/// Le report des factures négatives : une facture à net négatif d'un vendeur ordinaire naît « Reportée », jamais réglée,
/// et sa prochaine facture porte la ligne « Report FA-… : −X MAD ». Lignes sans frais : TVA nulle quel que soit le taux du jour.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CarryOverContractTests(ApiFactory factory)
{
    private string Today => factory.Services.GetRequiredService<IClock>().Today();

    /// <summary>Un brouillon dont chaque ligne est un colis neuf : `Cod` encaissé, `Cheque` déjà payé par chèque, aucun frais.</summary>
    private async Task<string> DraftAsync(DeliveryNotesContractTests.World world, params (long Cod, long Cheque)[] lines)
    {
        var code = $"FA-T{Cuid.New()[^10..]}";
        var parcels = new List<(string Id, string Code)>();
        foreach (var _ in lines) parcels.Add(await DeliveryNotesContractTests.ParcelAsync(world));
        await factory.ExecuteDbAsync(async db =>
        {
            var invoice = new Invoice { Code = code, ClientId = world.Merchant.Id, AgencyId = world.Agency.Id, CreatedAt = DateTime.UtcNow };
            db.Invoices.Add(invoice);
            for (var i = 0; i < lines.Length; i++)
            {
                db.InvoiceLines.Add(new InvoiceLine
                {
                    InvoiceId = invoice.Id, ParcelId = parcels[i].Id, ParcelCode = parcels[i].Code, StatusId = 4, CodAmount = Money.FromCents(lines[i].Cod), DeliveryFee = 0m,
                    ChequePaid = Money.FromCents(lines[i].Cheque), NetDue = Money.FromCents(lines[i].Cod - lines[i].Cheque),
                });
            }
            await db.SaveChangesAsync();
        });
        return code;
    }

    private static async Task<JsonElement> IssueAsync(DeliveryNotesContractTests.World world, string code)
    {
        var response = await world.Admin.PostAsJsonAsync($"/api/invoices/{code}/issue", new { });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.JsonAsync();
    }

    private static Task<HttpResponseMessage> PayAsync(DeliveryNotesContractTests.World world, JsonElement invoice, double amount) =>
        world.Admin.PostAsJsonAsync("/api/payments", new { invoiceId = invoice.GetProperty("id").GetString(), clientId = world.Merchant.Id, agencyId = world.Agency.Id, amount });

    private static List<JsonElement> CarryLines(JsonElement invoice) =>
        invoice.GetProperty("adjustments").EnumerateArray().Where(a => a.GetProperty("kind").GetString() == "carry-over").ToList();

    [Fact]
    public async Task Minus_65_is_carried_over_then_plus_599_issues_at_534_with_the_carry_line()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var negative = await DraftAsync(world, (0, 6500));
        var carried = await IssueAsync(world, negative);
        carried.GetProperty("status").GetString().ShouldBe("carried-over");
        carried.GetProperty("totalDue").GetInt64().ShouldBe(-6500);
        carried.GetProperty("balance").GetInt64().ShouldBe(0);
        carried.GetProperty("carriedIntoCode").ValueKind.ShouldBe(JsonValueKind.Null);
        var parcelId = carried.GetProperty("lines")[0].GetProperty("parcelId").GetString();
        (await factory.QueryDbAsync(db => db.Parcels.Where(p => p.Id == parcelId).Select(p => p.FinancialStatus).SingleAsync())).ShouldBe(FinancialStatus.REMBOURSE);

        var refused = await PayAsync(world, carried, 65);
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await refused.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("invoice-negative-carried-over");

        var filtered = await (await world.Admin.GetAsync($"/api/invoices?status=carried-over&clientId={world.Merchant.Id}")).JsonAsync();
        filtered.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("code").GetString()).ShouldBe([negative]);
        (await world.Seller.GetAsync($"/api/invoices/{negative}")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var next = await DraftAsync(world, (59900, 0));
        var absorbed = await IssueAsync(world, next);
        absorbed.GetProperty("status").GetString().ShouldBe("issued");
        absorbed.GetProperty("totalDue").GetInt64().ShouldBe(53400);
        var line = CarryLines(absorbed).Single();
        line.GetProperty("amount").GetInt64().ShouldBe(6500);
        line.GetProperty("vatAmount").GetInt64().ShouldBe(0);
        line.GetProperty("reason").GetString().ShouldBe($"Report {negative} : −65,00 MAD");
        line.GetProperty("reference").GetString().ShouldBe(negative);

        var source = await (await world.Admin.GetAsync($"/api/invoices/{negative}")).JsonAsync();
        source.GetProperty("status").GetString().ShouldBe("carried-over");
        source.GetProperty("carriedIntoCode").GetString().ShouldBe(next);
        (await world.Seller.GetAsync($"/api/invoices/{negative}/pdf")).Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        (await world.Admin.GetAsync($"/api/invoices/{negative}/export")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // En-tête du jour : la facture « Reportée » ne compte qu'une fois, sur celle qui l'absorbe.
        var days = await (await world.Admin.GetAsync($"/api/invoices/days?days={Today}&clientId={world.Merchant.Id}")).JsonAsync();
        days[0].GetProperty("totalDue").GetInt64().ShouldBe(53400);

        // Plus rien en attente : la facture suivante ne reprend pas le report.
        var third = await IssueAsync(world, await DraftAsync(world, (10000, 0)));
        third.GetProperty("totalDue").GetInt64().ShouldBe(10000);
        CarryLines(third).ShouldBeEmpty();
    }

    [Fact]
    public async Task Two_pending_carry_overs_add_up_599_minus_40_minus_65_is_494()
    {
        // Deux reports en attente en même temps : le cas du rattrapage (PAPY JOE, −40 et −65 remis « Reportée »).
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var codes = new[] { $"FA-T{Cuid.New()[^10..]}", $"FA-T{Cuid.New()[^10..]}" };
        await factory.ExecuteDbAsync(async db =>
        {
            var at = DateTime.UtcNow.AddHours(-2);
            foreach (var (code, due) in codes.Zip(new[] { -40m, -65m }))
            {
                db.Invoices.Add(new Invoice
                {
                    Code = code, ClientId = world.Merchant.Id, AgencyId = world.Agency.Id, Status = InvoiceStatus.CARRIED_OVER, TotalDue = due, IssuedAt = at, CreatedAt = at,
                });
                at = at.AddMinutes(1);
            }
            await db.SaveChangesAsync();
        });
        var next = await IssueAsync(world, await DraftAsync(world, (59900, 0)));
        next.GetProperty("totalDue").GetInt64().ShouldBe(49400);
        CarryLines(next).Select(l => l.GetProperty("amount").GetInt64()).ShouldBe([4000, 6500]);
        CarryLines(next).Select(l => l.GetProperty("reference").GetString()).ShouldBe(codes);

        // Émises l'une après l'autre, la seconde absorbe la première : −105 reporté d'un bloc, même total.
        var chained = await DeliveryNotesContractTests.WorldAsync(factory);
        await IssueAsync(chained, await DraftAsync(chained, (0, 4000)));
        var second = await IssueAsync(chained, await DraftAsync(chained, (0, 6500)));
        second.GetProperty("totalDue").GetInt64().ShouldBe(-10500);
        (await IssueAsync(chained, await DraftAsync(chained, (59900, 0)))).GetProperty("totalDue").GetInt64().ShouldBe(49400);
    }

    [Fact]
    public async Task A_next_invoice_still_negative_is_carried_over_in_turn()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var first = await DraftAsync(world, (0, 6500));
        await IssueAsync(world, first);
        var small = await DraftAsync(world, (1000, 0));
        var still = await IssueAsync(world, small);
        still.GetProperty("totalDue").GetInt64().ShouldBe(-5500);
        still.GetProperty("status").GetString().ShouldBe("carried-over");
        CarryLines(still).Single().GetProperty("reference").GetString().ShouldBe(first);

        var last = await IssueAsync(world, await DraftAsync(world, (59900, 0)));
        last.GetProperty("totalDue").GetInt64().ShouldBe(54400);
        CarryLines(last).Single().GetProperty("reason").GetString().ShouldBe($"Report {small} : −55,00 MAD");
        var into = await factory.QueryDbAsync(db => db.Invoices.Where(i => i.Code == first).Select(i => i.CarriedInto!.Code).SingleAsync());
        into.ShouldBe(small);
    }

    [Fact]
    public async Task Pro_internal_and_professional_regime_are_never_carried_over_and_keep_settling_their_fees()
    {
        var pro = await DeliveryNotesContractTests.WorldAsync(factory);
        await factory.ExecuteDbAsync(async db =>
        {
            (await db.Clients.SingleAsync(c => c.Id == pro.Merchant.Id)).IsPro = true;
            await db.SaveChangesAsync();
        });
        var proInvoice = await IssueAsync(pro, await DraftAsync(pro, (0, 6500)));
        proInvoice.GetProperty("status").GetString().ShouldBe("issued");
        (await PayAsync(pro, proInvoice, 65)).StatusCode.ShouldBe(HttpStatusCode.Created);

        var own = await DeliveryNotesContractTests.WorldAsync(factory);
        var url = $"/api/clients/{own.Merchant.Id}/internal";
        (await own.Seller.PatchAsJsonAsync(url, new { isInternal = true })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await own.Admin.PatchAsJsonAsync(url, new { })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var marked = await own.Admin.PatchAsJsonAsync(url, new { isInternal = true });
        marked.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await marked.JsonAsync()).GetProperty("isInternal").GetBoolean().ShouldBeTrue();
        var journal = await factory.QueryDbAsync(db => db.AuditLogs.Where(a => a.Entity == "Client" && a.EntityId == own.Merchant.Id && a.Action == AuditAction.UPDATE)
            .Select(a => a.Diff).ToListAsync());
        journal.ShouldContain(d => d.Contains("isInternal"));
        var ownInvoice = await IssueAsync(own, await DraftAsync(own, (0, 6500)));
        ownInvoice.GetProperty("status").GetString().ShouldBe("issued");
        (await PayAsync(own, ownInvoice, 65)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await (await own.Admin.GetAsync($"/api/invoices/{ownInvoice.GetProperty("code").GetString()}")).JsonAsync()).GetProperty("status").GetString().ShouldBe("paid");

        var professional = await DeliveryNotesContractTests.WorldAsync(factory);
        var code = await DraftAsync(professional, (0, 0));
        await factory.ExecuteDbAsync(async db =>
        {
            (await db.Clients.SingleAsync(c => c.Id == professional.Merchant.Id)).BillingType = BillingType.PROFESSIONAL;
            var invoice = await db.Invoices.SingleAsync(i => i.Code == code);
            invoice.BillingType = BillingType.PROFESSIONAL;
            db.InvoiceAdjustments.Add(new InvoiceAdjustment { InvoiceId = invoice.Id, Kind = InvoiceAdjustmentKind.OTHER, Amount = 65m, Reason = "Frais", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });
        var fees = await IssueAsync(professional, code);
        fees.GetProperty("totalDue").GetInt64().ShouldBeLessThan(0);
        fees.GetProperty("status").GetString().ShouldBe("issued");
    }

    [Fact]
    public async Task Payment_is_refused_on_an_issued_negative_invoice_of_an_ordinary_seller()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var code = await DraftAsync(world, (0, 6500));
        await factory.ExecuteDbAsync(async db =>
        {
            var row = await db.Invoices.SingleAsync(i => i.Code == code);
            row.Status = InvoiceStatus.ISSUED;
            row.IssuedAt = DateTime.UtcNow;
            row.TotalDue = -65m;
            await db.SaveChangesAsync();
        });
        var invoice = await (await world.Admin.GetAsync($"/api/invoices/{code}")).JsonAsync();
        var refused = await PayAsync(world, invoice, 65);
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await refused.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("invoice-negative-carried-over");
    }

    [Fact]
    public async Task A_cancelled_payment_leaves_every_sum_and_list()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var issued = await IssueAsync(world, await DraftAsync(world, (10000, 0)));
        var code = issued.GetProperty("code").GetString()!;
        (await PayAsync(world, issued, 40)).StatusCode.ShouldBe(HttpStatusCode.Created);
        var invoiceId = issued.GetProperty("id").GetString();
        await factory.ExecuteDbAsync(async db =>
            await db.Payments.Where(p => p.InvoiceId == invoiceId).ExecuteUpdateAsync(s => s.SetProperty(p => p.CancelledAt, DateTime.UtcNow).SetProperty(p => p.CancelReason, "Test")));
        var detail = await (await world.Admin.GetAsync($"/api/invoices/{code}")).JsonAsync();
        detail.GetProperty("totalPaid").GetInt64().ShouldBe(0);
        detail.GetProperty("payments").GetArrayLength().ShouldBe(0);
        detail.GetProperty("balance").GetInt64().ShouldBe(10000);
        (await (await world.Seller.GetAsync("/api/payments")).JsonAsync()).GetProperty("total").GetInt32().ShouldBe(0);
        (await factory.QueryDbAsync(db => db.Payments.IgnoreQueryFilters().CountAsync(p => p.InvoiceId == invoiceId))).ShouldBe(1);
    }

    [Fact]
    public async Task Transfer_selection_already_deducts_a_pending_carry_over()
    {
        var (world, _, phone, _) = await RemittancesContractTests.DeliveredAsync(factory);
        var remittance = (await (await phone.GetAsync("/api/courier/me/remittance")).JsonAsync()).GetProperty("remittance").GetProperty("code").GetString()!;
        (await world.Admin.PostAsync($"/api/remittances/{remittance}/confirm", null)).StatusCode.ShouldBe(HttpStatusCode.Created);

        async Task<long> CandidateDueAsync() => (await (await world.Admin.GetAsync("/api/invoices/bulk-candidates")).JsonAsync()).EnumerateArray()
            .Single(c => c.GetProperty("clientId").GetString() == world.Merchant.Id).GetProperty("totalDue").GetInt64();

        var before = await CandidateDueAsync();
        await factory.ExecuteDbAsync(async db =>
        {
            db.Invoices.Add(new Invoice
            {
                Code = $"FA-T{Cuid.New()[^10..]}", ClientId = world.Merchant.Id, AgencyId = world.Agency.Id, Status = InvoiceStatus.CARRIED_OVER, TotalDue = -65m,
                IssuedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });
        (await CandidateDueAsync()).ShouldBe(before - 6500);
    }
}
