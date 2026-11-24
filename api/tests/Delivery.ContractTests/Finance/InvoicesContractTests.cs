using System.Net;
using System.Net.Http.Json;
using Delivery.Application.Common.Interfaces;
using Delivery.ContractTests.Infrastructure;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.ContractTests.Finance;

/// <summary>`InvoicesController`, `PaymentsController`, `TransferOrdersController` : de la facture brouillon au virement.</summary>
[Collection(ApiCollection.Name)]
public sealed class InvoicesContractTests(ApiFactory factory)
{
    private string Today => factory.Services.GetRequiredService<IClock>().Today();

    /// <summary>Un colis livré dont la caisse est confirmée (cash remis par le livreur).</summary>
    private async Task<(Routes.DeliveryNotesContractTests.World World, (string Id, string Code) Parcel)> RemittedAsync()
    {
        var (world, _, phone, parcel) = await RemittancesContractTests.DeliveredAsync(factory);
        var code = (await (await phone.GetAsync("/api/courier/me/remittance")).JsonAsync()).GetProperty("remittance").GetProperty("code").GetString()!;
        (await world.Admin.PostAsync($"/api/remittances/{code}/confirm", null)).StatusCode.ShouldBe(HttpStatusCode.Created);
        return (world, parcel);
    }

    [Fact]
    public async Task Invoice_is_composed_issued_paid_and_transferred_once()
    {
        var (world, parcel) = await RemittedAsync();
        var billable = await (await world.Admin.GetAsync($"/api/invoices/billable?clientId={world.Merchant.Id}")).JsonAsync();
        var row = billable.EnumerateArray().Single(p => p.GetProperty("id").GetString() == parcel.Id);
        row.GetProperty("cashReceived").GetBoolean().ShouldBeTrue();
        row.GetProperty("billability").GetString().ShouldBe("billable");
        (await world.Seller.GetAsync($"/api/invoices/billable?clientId={world.Merchant.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var draft = await (await world.Admin.PostAsJsonAsync("/api/invoices", new { clientId = world.Merchant.Id, agencyId = world.Agency.Id })).JsonAsync();
        var code = draft.GetProperty("code").GetString()!;
        code.ShouldStartWith("FA");
        var second = await world.Admin.PostAsJsonAsync("/api/invoices", new { clientId = world.Merchant.Id, agencyId = world.Agency.Id });
        (await second.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("draft-exists");
        (await world.Admin.PostAsJsonAsync("/api/invoices-pro", new { clientId = world.Merchant.Id, agencyId = world.Agency.Id })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var filled = await (await world.Admin.PostAsJsonAsync($"/api/invoices/{code}/parcels", new { parcelIds = new[] { parcel.Id } })).JsonAsync();
        filled.GetProperty("parcelCount").GetInt32().ShouldBe(1);
        var line = filled.GetProperty("lines")[0];
        var cod = line.GetProperty("codAmount").GetInt64();
        var fee = line.GetProperty("deliveryFee").GetInt64();
        (cod - fee - line.GetProperty("vatAmount").GetInt64()).ShouldBe(line.GetProperty("netDue").GetInt64());
        var adjusted = await (await world.Admin.PostAsJsonAsync($"/api/invoices/{code}/adjustments", new { amount = 10, reason = "Régularisation" })).JsonAsync();
        adjusted.GetProperty("totalAdjustment").GetInt64().ShouldBe(1000);
        var due = adjusted.GetProperty("totalDue").GetInt64();
        (await world.Admin.DeleteAsync($"/api/invoices/{code}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await world.Seller.GetAsync($"/api/invoices/{code}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var issued = await (await world.Admin.PostAsJsonAsync($"/api/invoices/{code}/issue", new { dueAt = Today })).JsonAsync();
        issued.GetProperty("status").GetString().ShouldBe("issued");
        issued.GetProperty("totalDue").GetInt64().ShouldBe(due);
        (await world.Admin.PostAsJsonAsync($"/api/invoices/{code}/issue", new { })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var state = await factory.QueryDbAsync(db => db.Parcels.Where(p => p.Id == parcel.Id).Select(p => new { p.BillingState, p.FinancialStatus }).SingleAsync());
        state.BillingState.ShouldBe(BillingState.INVOICED);
        state.FinancialStatus.ShouldBe(FinancialStatus.FACTURE);
        (await world.Seller.GetAsync($"/api/invoices/{code}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await world.Seller.GetAsync($"/api/invoices/{code}/pdf")).Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        (await world.Admin.GetAsync($"/api/invoices/{code}/export")).Content.Headers.ContentDisposition!.FileName!.Trim('"').ShouldBe($"Facture-{code}.xlsx");
        var topics = await factory.QueryDbAsync(db => db.Notifications.Where(n => n.UserId == world.Merchant.UserId).Select(n => n.Topic).ToListAsync());
        topics.ShouldContain("invoice-issued");

        var days = await (await world.Admin.GetAsync($"/api/invoices/days?days={Today}&clientId={world.Merchant.Id}")).JsonAsync();
        days[0].GetProperty("invoiceCount").GetInt32().ShouldBe(1);
        days[0].GetProperty("transferableCodes")[0].GetString().ShouldBe(code);
        (await world.Admin.GetAsync($"/api/invoices/day-pdf?day={Today}&clientId={world.Merchant.Id}")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var half = due / 2;
        var invoiceId = issued.GetProperty("id").GetString()!;
        var first = await world.Admin.PostAsJsonAsync("/api/payments", new { invoiceId, clientId = world.Merchant.Id, agencyId = world.Agency.Id, amount = half / 100.0 });
        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await (await world.Admin.GetAsync($"/api/invoices/{code}")).JsonAsync()).GetProperty("balance").GetInt64().ShouldBe(due - half);
        var rest = await (await world.Admin.PostAsJsonAsync("/api/payments", new { invoiceId, clientId = world.Merchant.Id, agencyId = world.Agency.Id, amount = (due - half) / 100.0 })).JsonAsync();
        (await (await world.Admin.GetAsync($"/api/invoices/{code}")).JsonAsync()).GetProperty("status").GetString().ShouldBe("paid");
        (await world.Admin.PostAsJsonAsync("/api/payments", new { invoiceId, clientId = world.Merchant.Id, agencyId = world.Agency.Id, amount = 1 })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await world.Admin.DeleteAsync($"/api/payments/{rest.GetProperty("id").GetString()}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await (await world.Admin.GetAsync($"/api/invoices/{code}")).JsonAsync()).GetProperty("status").GetString().ShouldBe("issued");
        (await (await world.Seller.GetAsync("/api/payments")).JsonAsync()).GetProperty("total").GetInt32().ShouldBe(1);

        var order = await world.Admin.PostAsJsonAsync("/api/transfer-orders", new { invoiceCodes = new[] { code } });
        order.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await order.JsonAsync();
        body.GetProperty("code").GetString()!.ShouldStartWith("OV");
        body.GetProperty("source").GetString().ShouldBe("invoices");
        body.GetProperty("totalDue").GetInt64().ShouldBe(due);
        var twice = await world.Admin.PostAsJsonAsync("/api/transfer-orders", new { invoiceCodes = new[] { code } });
        (await twice.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("invoice-already-transferred");
        (await world.Admin.PostAsJsonAsync("/api/transfer-orders", new { })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var ov = body.GetProperty("code").GetString()!;
        (await world.Admin.GetAsync($"/api/transfer-orders/{ov}/pdf")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await world.Admin.GetAsync($"/api/transfer-orders/{ov}/xlsx")).Content.Headers.ContentDisposition!.FileName!.Trim('"').ShouldStartWith("Ordre-Virement-");
    }

    [Fact]
    public async Task Bulk_invoice_skips_a_cod_already_paid_out_for_a_normal_client()
    {
        var (world, parcel) = await RemittedAsync();
        await factory.ExecuteDbAsync(async db =>
        {
            (await db.Clients.SingleAsync(c => c.Id == world.Merchant.Id)).BillingType = BillingType.PROFESSIONAL;
            await db.SaveChangesAsync();
        });
        var payout = (await (await world.Admin.PostAsJsonAsync("/api/cod-payouts", new { clientId = world.Merchant.Id, agencyId = world.Agency.Id })).JsonAsync()).GetProperty("code").GetString()!;
        await world.Admin.PostAsJsonAsync($"/api/cod-payouts/{payout}/parcels", new { parcelIds = new[] { parcel.Id } });
        (await world.Admin.PostAsJsonAsync($"/api/cod-payouts/{payout}/pay", new { method = "cash" })).StatusCode.ShouldBe(HttpStatusCode.Created);
        await factory.ExecuteDbAsync(async db =>
        {
            (await db.Clients.SingleAsync(c => c.Id == world.Merchant.Id)).BillingType = BillingType.NORMAL;
            await db.SaveChangesAsync();
        });

        (await (await world.Admin.GetAsync("/api/invoices/bulk-candidates")).JsonAsync()).EnumerateArray().Select(c => c.GetProperty("clientId").GetString()).ShouldNotContain(world.Merchant.Id);
        var result = await (await world.Admin.PostAsJsonAsync("/api/invoices/bulk", new { clientIds = new[] { world.Merchant.Id } })).JsonAsync();
        result.GetProperty("created").GetArrayLength().ShouldBe(1);
        var lines = await factory.QueryDbAsync(db => db.InvoiceLines.CountAsync(l => l.ParcelId == parcel.Id));
        lines.ShouldBe(0);
    }
}
