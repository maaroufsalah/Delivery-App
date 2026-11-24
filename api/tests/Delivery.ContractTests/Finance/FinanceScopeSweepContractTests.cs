using System.Net;
using System.Net.Http.Json;
using Delivery.ContractTests.Infrastructure;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.ContractTests.Finance;

/// <summary>Balayage de portée du module « Caisse et chèques » : livreur, vendeur et agent ne voient que leur part.</summary>
[Collection(ApiCollection.Name)]
public sealed class FinanceScopeSweepContractTests(ApiFactory factory)
{
    [Fact]
    public async Task Cash_cheques_and_petty_cash_stay_within_their_owner()
    {
        var a = await RemittancesContractTests.DeliveredAsync(factory);
        var b = await RemittancesContractTests.DeliveredAsync(factory);
        var remittanceA = (await (await a.Phone.GetAsync("/api/courier/me/remittance")).JsonAsync()).GetProperty("remittance").GetProperty("code").GetString()!;
        var chequeA = await factory.QueryDbAsync(db => db.Cheques.Where(c => c.CourierId == a.Courier.Id).Select(c => c.Id).FirstOrDefaultAsync());

        // Livreur B : ni la caisse, ni le solde, ni les candidats du livreur A.
        (await b.Phone.GetAsync($"/api/remittances/{remittanceA}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await b.Phone.GetAsync($"/api/remittances/{remittanceA}/pdf")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await (await b.Phone.GetAsync($"/api/remittances/candidates?courierId={a.Courier.Id}")).JsonAsync()).EnumerateArray()
            .Select(c => c.GetProperty("id").GetString()).ShouldNotContain(a.Parcel.Id);
        (await (await b.Phone.GetAsync("/api/courier/me/balance")).JsonAsync()).GetProperty("courierId").GetString().ShouldBe(b.Courier.Id);
        (await (await b.Phone.GetAsync("/api/cheques")).JsonAsync()).GetProperty("items").EnumerateArray().Select(c => c.GetProperty("courierId").GetString()).ShouldNotContain(a.Courier.Id);
        if (chequeA is not null) (await b.Phone.GetAsync($"/api/cheques/{chequeA}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Vendeur : aucune caisse, aucun tableau financier, aucun solde livreur.
        foreach (var path in new[] { "/api/remittances", $"/api/remittances/{remittanceA}", "/api/finance/dashboard", "/api/finance/overview", $"/api/couriers/{a.Courier.Id}/balance",
                     "/api/bank-accounts" })
        {
            (await a.World.Seller.GetAsync(path)).StatusCode.ShouldBe(HttpStatusCode.Forbidden, path);
        }
        (await (await b.World.Seller.GetAsync("/api/cheques")).JsonAsync()).GetProperty("total").GetInt32().ShouldBe(0);

        // Agent de l'agence B : ni la caisse ni la petite caisse de l'agence A.
        var agentB = await TestData.ClientForAsync(factory, await TestData.UserAsync(factory, RoleName.STAFF, b.World.Agency.Id));
        var list = await agentB.GetAsync("/api/remittances");
        if (list.StatusCode == HttpStatusCode.OK)
        {
            (await list.JsonAsync()).GetProperty("items").EnumerateArray().Select(r => r.GetProperty("code").GetString()).ShouldNotContain(remittanceA);
            (await agentB.GetAsync($"/api/remittances/{remittanceA}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }
        else
        {
            list.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
        await a.World.Admin.PostAsJsonAsync("/api/petty-cash", new { kind = "supply", date = DateTime.UtcNow.ToString("yyyy-MM-dd"), amount = 100, agencyId = a.World.Agency.Id });
        var petty = await agentB.GetAsync($"/api/petty-cash?agencyId={a.World.Agency.Id}&period=all");
        if (petty.StatusCode == HttpStatusCode.OK) (await petty.JsonAsync()).GetProperty("total").GetInt32().ShouldBe(0);
        else petty.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var write = await agentB.PostAsJsonAsync("/api/petty-cash", new { kind = "supply", date = DateTime.UtcNow.ToString("yyyy-MM-dd"), amount = 1, agencyId = a.World.Agency.Id });
        write.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
