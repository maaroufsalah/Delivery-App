using System.Net;
using System.Net.Http.Json;
using Delivery.Application.Common.Interfaces;
using Delivery.ContractTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.ContractTests.Finance;

/// <summary>Balayage de portée du module « Facturation » : factures, versements, relevés et règlements restent chez leur propriétaire.</summary>
[Collection(ApiCollection.Name)]
public sealed class BillingScopeSweepContractTests(ApiFactory factory)
{
    [Fact]
    public async Task Invoices_payouts_and_statements_stay_within_their_owner()
    {
        var a = await RemittancesContractTests.DeliveredAsync(factory);
        var b = await RemittancesContractTests.DeliveredAsync(factory);
        var today = factory.Services.GetRequiredService<IClock>().Today();
        var remittance = (await (await a.Phone.GetAsync("/api/courier/me/remittance")).JsonAsync()).GetProperty("remittance").GetProperty("code").GetString()!;
        await a.World.Admin.PostAsync($"/api/remittances/{remittance}/confirm", null);
        var invoice = (await (await a.World.Admin.PostAsJsonAsync("/api/invoices", new { clientId = a.World.Merchant.Id, agencyId = a.World.Agency.Id })).JsonAsync()).GetProperty("code").GetString()!;
        await a.World.Admin.PostAsJsonAsync($"/api/invoices/{invoice}/parcels", new { parcelIds = new[] { a.Parcel.Id } });
        await a.World.Admin.PostAsJsonAsync($"/api/invoices/{invoice}/issue", new { });
        var statement = (await (await a.World.Admin.PostAsJsonAsync("/api/courier-statements",
            new { courierId = a.Courier.Id, agencyId = a.World.Agency.Id, periodStart = today, periodEnd = today })).JsonAsync()).GetProperty("code").GetString()!;

        // Vendeur B : ni la facture, ni son PDF, ni son export.
        foreach (var path in new[] { $"/api/invoices/{invoice}", $"/api/invoices/{invoice}/pdf", $"/api/invoices/{invoice}/export" })
        {
            (await b.World.Seller.GetAsync(path)).StatusCode.ShouldBe(HttpStatusCode.NotFound, path);
        }
        (await (await b.World.Seller.GetAsync("/api/invoices")).JsonAsync()).GetProperty("total").GetInt32().ShouldBe(0);
        foreach (var path in new[] { "/api/invoices-pro", "/api/courier-statements", "/api/invoices/bulk-candidates" })
        {
            (await b.World.Seller.GetAsync(path)).StatusCode.ShouldBeOneOf(HttpStatusCode.Forbidden);
        }

        // Livreur B : ni le relevé du livreur A, ni les factures.
        (await b.Phone.GetAsync($"/api/courier-statements/{statement}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await (await b.Phone.GetAsync("/api/courier-statements")).JsonAsync()).GetProperty("total").GetInt32().ShouldBe(0);
        var courierInvoices = await b.Phone.GetAsync("/api/invoices");
        if (courierInvoices.StatusCode == HttpStatusCode.OK) (await courierInvoices.JsonAsync()).GetProperty("total").GetInt32().ShouldBe(0);
        else courierInvoices.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
