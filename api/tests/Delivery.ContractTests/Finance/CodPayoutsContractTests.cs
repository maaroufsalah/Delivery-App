using System.Net;
using System.Net.Http.Json;
using Delivery.ContractTests.Infrastructure;
using Delivery.ContractTests.Routes;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.ContractTests.Finance;

/// <summary>`CodPayoutsController` : le COD rendu au vendeur professionnel, chèque déduit, versé une seule fois.</summary>
[Collection(ApiCollection.Name)]
public sealed class CodPayoutsContractTests(ApiFactory factory)
{
    /// <summary>Un colis livré (300 MAD dont 100 par chèque), caisse confirmée, vendeur professionnel.</summary>
    private async Task<(DeliveryNotesContractTests.World World, (string Id, string Code) Parcel, long Collected)> RemittedAsync(bool professional = true)
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var (courier, phone) = await RoutesContractTests.CourierAsync(factory, world.City.Id);
        await factory.ExecuteDbAsync(async db =>
        {
            db.Couriers.Attach(courier);
            courier.AgencyId = world.Agency.Id;
            db.CourierCities.Add(new CourierCity { CourierId = courier.Id, CityId = world.City.Id, DeliveryFee = 15m });
            (await db.Clients.SingleAsync(c => c.Id == world.Merchant.Id)).BillingType = professional ? BillingType.PROFESSIONAL : BillingType.NORMAL;
            await db.SaveChangesAsync();
        });
        var parcel = await DeliveryNotesContractTests.ParcelAsync(world, 300);
        await RoutesContractTests.ReadyAsync(factory, parcel.Id);
        var route = (await (await world.Admin.PostAsJsonAsync("/api/routes", new { cityId = world.City.Id, courierId = courier.Id })).JsonAsync()).GetProperty("code").GetString()!;
        await world.Admin.PostAsJsonAsync($"/api/routes/{route}/parcels", new { parcelIds = new[] { parcel.Id } });
        await world.Admin.PostAsync($"/api/routes/{route}/dispatch", null);
        (await phone.PostAsJsonAsync($"/api/courier/parcels/{parcel.Code}/status", new { statusId = 4, chequeAmount = 100, chequeNumber = "CH-77", chequeBank = "BMCE" })).StatusCode
            .ShouldBe(HttpStatusCode.OK);
        var remittance = (await (await phone.GetAsync("/api/courier/me/remittance")).JsonAsync()).GetProperty("remittance");
        var collected = remittance.GetProperty("expectedCod").GetInt64();
        (await world.Admin.PostAsync($"/api/remittances/{remittance.GetProperty("code").GetString()}/confirm", null)).StatusCode.ShouldBe(HttpStatusCode.Created);
        return (world, parcel, collected);
    }

    [Fact]
    public async Task Payout_pays_the_cash_part_once_and_leaves_the_cheque_out()
    {
        var (world, parcel, collected) = await RemittedAsync();

        var candidates = await (await world.Admin.GetAsync($"/api/cod-payouts/candidates?clientId={world.Merchant.Id}")).JsonAsync();
        var candidate = candidates.EnumerateArray().Single(c => c.GetProperty("id").GetString() == parcel.Id);
        candidate.GetProperty("codCollected").GetInt64().ShouldBe(collected - 10000);

        var draft = await (await world.Admin.PostAsJsonAsync("/api/cod-payouts", new { clientId = world.Merchant.Id, agencyId = world.Agency.Id })).JsonAsync();
        var code = draft.GetProperty("code").GetString()!;
        code.ShouldStartWith("BV");
        draft.GetProperty("payable").GetBoolean().ShouldBeFalse();
        var filled = await (await world.Admin.PostAsJsonAsync($"/api/cod-payouts/{code}/parcels", new { parcelIds = new[] { parcel.Id } })).JsonAsync();
        filled.GetProperty("amount").GetInt64().ShouldBe(collected - 10000);
        filled.GetProperty("cheques")[0].GetProperty("number").GetString().ShouldBe("CH-77");
        filled.GetProperty("cheques")[0].GetProperty("includedInPayout").GetBoolean().ShouldBeFalse();

        (await world.Admin.PostAsJsonAsync($"/api/cod-payouts/{code}/pay", new { })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var paid = await world.Admin.PostAsJsonAsync($"/api/cod-payouts/{code}/pay", new { method = "transfer", reference = "VIR-12" });
        paid.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await paid.JsonAsync();
        body.GetProperty("status").GetString().ShouldBe("paid");
        body.GetProperty("method").GetString().ShouldBe("transfer");
        body.GetProperty("amount").GetInt64().ShouldBe(collected - 10000);
        (await world.Admin.PostAsJsonAsync($"/api/cod-payouts/{code}/pay", new { method = "cash" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await world.Admin.DeleteAsync($"/api/cod-payouts/{code}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var again = await (await world.Admin.PostAsJsonAsync("/api/cod-payouts", new { clientId = world.Merchant.Id, agencyId = world.Agency.Id })).JsonAsync();
        var twice = await world.Admin.PostAsJsonAsync($"/api/cod-payouts/{again.GetProperty("code").GetString()}/parcels", new { parcelIds = new[] { parcel.Id } });
        twice.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await twice.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("parcel-already-paid-out");
        (await (await world.Admin.GetAsync($"/api/cod-payouts/candidates?clientId={world.Merchant.Id}")).JsonAsync()).EnumerateArray()
            .Select(c => c.GetProperty("id").GetString()).ShouldNotContain(parcel.Id);

        var mine = await (await world.Seller.GetAsync("/api/cod-payouts")).JsonAsync();
        mine.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("code").GetString()).ShouldContain(code);
        (await world.Seller.GetAsync($"/api/cod-payouts/{code}/pdf")).Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        (await world.Admin.GetAsync($"/api/cod-payouts/{code}/export")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Bulk_opens_one_payout_per_professional_and_refuses_a_normal_client()
    {
        var (world, parcel, collected) = await RemittedAsync();
        var rows = await (await world.Admin.GetAsync($"/api/cod-payouts/bulk-candidates?agencyId={world.Agency.Id}")).JsonAsync();
        var row = rows.EnumerateArray().Single(r => r.GetProperty("clientId").GetString() == world.Merchant.Id);
        row.GetProperty("cash").GetInt64().ShouldBe(collected - 10000);
        row.GetProperty("chequeAmount").GetInt64().ShouldBe(10000);

        (await world.Admin.PostAsJsonAsync("/api/cod-payouts/bulk", new { })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var result = await (await world.Admin.PostAsJsonAsync("/api/cod-payouts/bulk", new { clientIds = new[] { world.Merchant.Id, "inconnu" } })).JsonAsync();
        result.GetProperty("created").GetArrayLength().ShouldBe(1);
        result.GetProperty("total").GetInt64().ShouldBe(collected - 10000);
        result.GetProperty("skipped")[0].GetProperty("reason").GetString().ShouldBe("client-unknown");
        var line = await factory.QueryDbAsync(db => db.CodPayoutParcels.Where(l => l.ParcelId == parcel.Id).Select(l => l.Amount).SingleAsync());
        line.ShouldBe((collected - 10000) / 100m);

        var normal = await RemittedAsync(professional: false);
        var refused = await normal.World.Admin.PostAsJsonAsync("/api/cod-payouts", new { clientId = normal.World.Merchant.Id, agencyId = normal.World.Agency.Id });
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await refused.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("client-not-professional");
    }
}
