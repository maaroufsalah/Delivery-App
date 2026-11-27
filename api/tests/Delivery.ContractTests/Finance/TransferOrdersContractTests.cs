using System.Net;
using System.Net.Http.Json;
using Delivery.Application.Common.Interfaces;
using Delivery.ContractTests.Infrastructure;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.ContractTests.Finance;

/// <summary>Ordres de virement : la liste (consultation seule), le lien OV ↔ factures, les factures à net 0 et à net négatif.</summary>
[Collection(ApiCollection.Name)]
public sealed class TransferOrdersContractTests(ApiFactory factory)
{
    private string Today => factory.Services.GetRequiredService<IClock>().Today();

    private async Task<(Routes.DeliveryNotesContractTests.World World, (string Id, string Code) Parcel)> RemittedAsync()
    {
        var (world, _, phone, parcel) = await RemittancesContractTests.DeliveredAsync(factory);
        var code = (await (await phone.GetAsync("/api/courier/me/remittance")).JsonAsync()).GetProperty("remittance").GetProperty("code").GetString()!;
        (await world.Admin.PostAsync($"/api/remittances/{code}/confirm", null)).StatusCode.ShouldBe(HttpStatusCode.Created);
        return (world, parcel);
    }

    private static async Task<(string Code, string Id)> IssuedAsync(Routes.DeliveryNotesContractTests.World world, string parcelId)
    {
        var code = (await (await world.Admin.PostAsJsonAsync("/api/invoices", new { clientId = world.Merchant.Id, agencyId = world.Agency.Id })).JsonAsync()).GetProperty("code").GetString()!;
        (await world.Admin.PostAsJsonAsync($"/api/invoices/{code}/parcels", new { parcelIds = new[] { parcelId } })).StatusCode.ShouldBe(HttpStatusCode.Created);
        var issued = await (await world.Admin.PostAsJsonAsync($"/api/invoices/{code}/issue", new { })).JsonAsync();
        return (code, issued.GetProperty("id").GetString()!);
    }

    private Task SetInvoiceAsync(string code, decimal totalDue, InvoiceStatus status) => factory.ExecuteDbAsync(db =>
        db.Invoices.Where(i => i.Code == code).ExecuteUpdateAsync(s => s.SetProperty(i => i.TotalDue, totalDue).SetProperty(i => i.Status, status)));

    private async Task<List<string>> TransferableAsync(Routes.DeliveryNotesContractTests.World world)
    {
        var days = await (await world.Admin.GetAsync($"/api/invoices/days?days={Today}&clientId={world.Merchant.Id}")).JsonAsync();
        return days[0].GetProperty("transferableCodes").EnumerateArray().Select(c => c.GetString()!).ToList();
    }

    [Fact]
    public async Task List_is_paginated_newest_first_searchable_by_number_and_reread_without_recalculation()
    {
        var world = await Routes.DeliveryNotesContractTests.WorldAsync(factory);
        var tag = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        // Un OV stocké dont les totaux ne sont pas la somme des lignes : il doit être relu tel quel.
        const string lines = """[{"clientId":"__CLIENT__","clientName":"SNOWDROP","parcelCount":2,"collected":4000,"fees":4000,"due":0}]""";
        await factory.ExecuteDbAsync(async db =>
        {
            foreach (var (suffix, at) in new[] { ("1", new DateTime(2099, 1, 1, 8, 0, 0, DateTimeKind.Utc)), ("2", new DateTime(2099, 1, 2, 8, 0, 0, DateTimeKind.Utc)) })
            {
                db.TransferOrders.Add(new TransferOrder
                {
                    Code = $"OV-T{tag}-{suffix}", Source = TransferOrderSource.SELECTION, AgencyId = world.Agency.Id, Lines = lines.Replace("__CLIENT__", world.Merchant.Id),
                    InvoiceCodes = ["FA-OLD-1"], TotalCollected = 12.34m, TotalFees = 5m, TotalDue = 7.34m, CreatedAt = at,
                });
            }
            await db.SaveChangesAsync();
        });

        var page = await (await world.Admin.GetAsync($"/api/transfer-orders?search={tag.ToLowerInvariant()}&pageSize=1")).JsonAsync();
        page.GetProperty("total").GetInt32().ShouldBe(2);
        page.GetProperty("page").GetInt32().ShouldBe(1);
        page.GetProperty("pageSize").GetInt32().ShouldBe(1);
        var first = page.GetProperty("items")[0];
        first.GetProperty("code").GetString().ShouldBe($"OV-T{tag}-2");
        first.GetProperty("createdAt").GetString().ShouldBe("2099-01-02T08:00:00.000Z");
        first.GetProperty("kind").GetString().ShouldBe("clients");
        first.GetProperty("clientCount").GetInt32().ShouldBe(1);
        first.GetProperty("totalDue").GetInt64().ShouldBe(734);
        first.GetProperty("createdByName").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        var second = await (await world.Admin.GetAsync($"/api/transfer-orders?search={tag}&pageSize=1&page=2")).JsonAsync();
        second.GetProperty("items")[0].GetProperty("code").GetString().ShouldBe($"OV-T{tag}-1");
        (await (await world.Admin.GetAsync($"/api/transfer-orders?search=OV-T{tag}-1")).JsonAsync()).GetProperty("total").GetInt32().ShouldBe(1);
        (await world.Admin.GetAsync("/api/transfer-orders?pageSize=0")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        await factory.ExecuteDbAsync(async db =>
        {
            (await db.Clients.SingleAsync(c => c.Id == world.Merchant.Id)).IsPro = true;
            await db.SaveChangesAsync();
        });
        (await (await world.Admin.GetAsync($"/api/transfer-orders?search={tag}")).JsonAsync()).GetProperty("items")[0].GetProperty("kind").GetString().ShouldBe("pro");

        var detail = await (await world.Admin.GetAsync($"/api/transfer-orders/OV-T{tag}-1")).JsonAsync();
        var line = detail.GetProperty("lines")[0];
        (line.GetProperty("collected").GetInt64(), line.GetProperty("fees").GetInt64(), line.GetProperty("due").GetInt64()).ShouldBe((4000L, 4000L, 0L));
        (detail.GetProperty("totalCollected").GetInt64(), detail.GetProperty("totalFees").GetInt64(), detail.GetProperty("totalDue").GetInt64()).ShouldBe((1234L, 500L, 734L));
        detail.GetProperty("invoiceCodes")[0].GetString().ShouldBe("FA-OLD-1");
        (await world.Admin.GetAsync($"/api/transfer-orders/OV-T{tag}-1/pdf")).Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        (await world.Admin.GetAsync($"/api/transfer-orders/OV-T{tag}-1/xlsx")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task List_requires_finance_write()
    {
        var world = await Routes.DeliveryNotesContractTests.WorldAsync(factory);
        (await world.Seller.GetAsync("/api/transfer-orders")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_selection_order_links_the_invoice_issued_afterwards_and_the_daily_button_no_longer_offers_it()
    {
        var (world, parcel) = await RemittedAsync();
        var created = await world.Admin.PostAsJsonAsync("/api/transfer-orders", new { clientIds = new[] { world.Merchant.Id } });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var ov = await created.JsonAsync();
        ov.GetProperty("source").GetString().ShouldBe("selection");
        ov.GetProperty("invoiceCodes").GetArrayLength().ShouldBe(0);
        var ovCode = ov.GetProperty("code").GetString()!;

        var (code, _) = await IssuedAsync(world, parcel.Id);
        var linked = await (await world.Admin.GetAsync($"/api/transfer-orders/{ovCode}")).JsonAsync();
        linked.GetProperty("invoiceCodes").EnumerateArray().Select(c => c.GetString()).ShouldBe([code]);
        (await TransferableAsync(world)).ShouldBeEmpty();
        var twice = await world.Admin.PostAsJsonAsync("/api/transfer-orders", new { invoiceCodes = new[] { code } });
        twice.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await twice.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("invoice-already-transferred");
    }

    [Fact]
    public async Task Net_zero_invoice_enters_the_order_at_zero_while_a_negative_one_never_does_and_its_payment_counts_negative()
    {
        var (negative, negativeParcel) = await RemittedAsync();
        var (minus, minusId) = await IssuedAsync(negative, negativeParcel.Id);
        await SetInvoiceAsync(minus, -65m, InvoiceStatus.ISSUED);
        (await TransferableAsync(negative)).ShouldBeEmpty();
        var refused = await negative.Admin.PostAsJsonAsync("/api/transfer-orders", new { invoiceCodes = new[] { minus } });
        (await refused.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("nothing-to-transfer");
        // Report des factures négatives : un vendeur ordinaire ne règle pas un net négatif ; un client interne, si.
        (await negative.Admin.PostAsJsonAsync("/api/payments", new { invoiceId = minusId, clientId = negative.Merchant.Id, agencyId = negative.Agency.Id, amount = 65, method = "cash" }))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await negative.Admin.PatchAsJsonAsync($"/api/clients/{negative.Merchant.Id}/internal", new { isInternal = true })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await negative.Admin.PostAsJsonAsync("/api/payments", new { invoiceId = minusId, clientId = negative.Merchant.Id, agencyId = negative.Agency.Id, amount = 65, method = "cash" }))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
        var day = (await (await negative.Admin.GetAsync($"/api/invoices/days?days={Today}&clientId={negative.Merchant.Id}")).JsonAsync())[0];
        day.GetProperty("totalDue").GetInt64().ShouldBe(-6500);
        day.GetProperty("paidCash").GetInt64().ShouldBe(-6500);

        var (zero, zeroParcel) = await RemittedAsync();
        var (nil, _) = await IssuedAsync(zero, zeroParcel.Id);
        await SetInvoiceAsync(nil, 0m, InvoiceStatus.PAID);
        (await TransferableAsync(zero)).ShouldBe([nil]);
        var created = await zero.Admin.PostAsJsonAsync("/api/transfer-orders", new { invoiceCodes = new[] { nil, minus } });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var ov = await created.JsonAsync();
        ov.GetProperty("invoiceCodes").EnumerateArray().Select(c => c.GetString()).ShouldBe([nil]);
        var line = ov.GetProperty("lines").EnumerateArray().Single();
        line.GetProperty("clientId").GetString().ShouldBe(zero.Merchant.Id);
        line.GetProperty("due").GetInt64().ShouldBe(0);
        line.GetProperty("collected").GetInt64().ShouldBe(line.GetProperty("fees").GetInt64());
        (ov.GetProperty("totalCollected").GetInt64() - ov.GetProperty("totalFees").GetInt64()).ShouldBe(ov.GetProperty("totalDue").GetInt64());
        (await TransferableAsync(zero)).ShouldBeEmpty();
    }
}
