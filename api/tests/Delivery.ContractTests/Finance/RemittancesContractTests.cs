using System.Net;
using System.Net.Http.Json;
using Delivery.ContractTests.Infrastructure;
using Delivery.ContractTests.Routes;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.ContractTests.Finance;

/// <summary>`RemittancesController` : la caisse du livreur, de la livraison à la confirmation par l'agence.</summary>
[Collection(ApiCollection.Name)]
public sealed class RemittancesContractTests(ApiFactory factory)
{
    internal static async Task<(DeliveryNotesContractTests.World World, Courier Courier, HttpClient Phone, (string Id, string Code) Parcel)> DeliveredAsync(ApiFactory factory)
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var (courier, phone) = await RoutesContractTests.CourierAsync(factory, world.City.Id);
        await factory.ExecuteDbAsync(async db =>
        {
            db.Couriers.Attach(courier);
            courier.AgencyId = world.Agency.Id;
            db.CourierCities.Add(new CourierCity { CourierId = courier.Id, CityId = world.City.Id, DeliveryFee = 15m });
            await db.SaveChangesAsync();
        });
        var parcel = await DeliveryNotesContractTests.ParcelAsync(world, 200);
        await RoutesContractTests.ReadyAsync(factory, parcel.Id);
        var route = (await (await world.Admin.PostAsJsonAsync("/api/routes", new { cityId = world.City.Id, courierId = courier.Id })).JsonAsync()).GetProperty("code").GetString()!;
        await world.Admin.PostAsJsonAsync($"/api/routes/{route}/parcels", new { parcelIds = new[] { parcel.Id } });
        await world.Admin.PostAsync($"/api/routes/{route}/dispatch", null);
        (await phone.PostAsJsonAsync($"/api/courier/parcels/{parcel.Code}/status", new { statusId = 4 })).StatusCode.ShouldBe(HttpStatusCode.OK);
        return (world, courier, phone, parcel);
    }

    [Fact]
    public async Task Courier_closes_his_cash_and_the_agency_confirms_it_with_a_variance()
    {
        var (world, courier, phone, parcel) = await DeliveredAsync(factory);

        var mine = (await (await phone.GetAsync("/api/courier/me/remittance")).JsonAsync()).GetProperty("remittance");
        var code = mine.GetProperty("code").GetString()!;
        mine.GetProperty("stage").GetString().ShouldBe("open");
        mine.GetProperty("items")[0].GetProperty("parcelCode").GetString().ShouldBe(parcel.Code);
        var net = mine.GetProperty("expectedNet").GetInt64();
        net.ShouldBeGreaterThan(0);
        (await world.Seller.GetAsync("/api/remittances")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await phone.PatchAsJsonAsync($"/api/remittances/{code}/count", new { declaredAmount = 1, expectedNet = 1 })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var closed = await phone.PostAsync("/api/courier/me/remittance/close", null);
        closed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await closed.JsonAsync()).GetProperty("remittance").GetProperty("stage").GetString().ShouldBe("closed");
        (await (await phone.GetAsync("/api/courier/me/remittance")).JsonAsync()).GetProperty("remittance").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        var listed = await (await world.Admin.GetAsync($"/api/remittances?stage=closed&courierId={courier.Id}")).JsonAsync();
        listed.GetProperty("items")[0].GetProperty("code").GetString().ShouldBe(code);
        (await world.Admin.PostAsync($"/api/remittances/{code}/reopen", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await phone.PostAsync("/api/courier/me/remittance/close", null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var handed = (net - 500) / 100.0;
        var expected = net / 100.0;
        (await world.Admin.PatchAsJsonAsync($"/api/remittances/{code}/count", new { declaredAmount = handed, expectedNet = expected })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var counted = await (await world.Admin.PatchAsJsonAsync($"/api/remittances/{code}/count", new { declaredAmount = handed, expectedNet = expected, varianceReason = "Billet manquant" })).JsonAsync();
        counted.GetProperty("declaredAmount").GetInt64().ShouldBe(net - 500);
        counted.GetProperty("variance").GetInt64().ShouldBe(-500);

        var balanceBefore = await (await world.Admin.GetAsync($"/api/couriers/{courier.Id}/balance")).JsonAsync();
        balanceBefore.GetProperty("balance").GetInt64().ShouldBe(0);
        var confirmed = await world.Admin.PostAsync($"/api/remittances/{code}/confirm", null);
        confirmed.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await confirmed.JsonAsync();
        body.GetProperty("status").GetString().ShouldBe("confirmed");
        body.GetProperty("stage").GetString().ShouldBe("received");
        body.GetProperty("amount").GetInt64().ShouldBe(net - 500);
        (await world.Admin.PostAsync($"/api/remittances/{code}/confirm", null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await world.Admin.PostAsync($"/api/remittances/{code}/reopen", null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await world.Admin.DeleteAsync($"/api/remittances/{code}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var state = await factory.QueryDbAsync(db => db.Parcels.Where(p => p.Id == parcel.Id).Select(p => new { p.FinancialStatus, p.ReimbursedByCourier }).SingleAsync());
        state.FinancialStatus.ShouldBe(FinancialStatus.REMIS_LIVREUR);
        state.ReimbursedByCourier.ShouldBeTrue();

        var owed = await (await phone.GetAsync("/api/courier/me/balance")).JsonAsync();
        owed.GetProperty("balance").GetInt64().ShouldBe(-500);
        owed.GetProperty("entries")[0].GetProperty("kind").GetString().ShouldBe("variance");
        owed.GetProperty("entries")[0].GetProperty("remittanceCode").GetString().ShouldBe(code);
        (await phone.PostAsJsonAsync($"/api/couriers/{courier.Id}/balance/settlements", new { amount = 500 })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await world.Admin.PostAsJsonAsync($"/api/couriers/{courier.Id}/balance/settlements", new { amount = 0 })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var settled = await (await world.Admin.PostAsJsonAsync($"/api/couriers/{courier.Id}/balance/settlements", new { amount = 500, method = "cash" })).JsonAsync();
        settled.GetProperty("balance").GetInt64().ShouldBe(0);

        var topics = await factory.QueryDbAsync(db => db.Notifications.Where(n => n.UserId == courier.UserId).Select(n => n.Topic).ToListAsync());
        topics.ShouldContain("remittance-confirmed");
        var summary = await (await world.Admin.GetAsync($"/api/remittances/summary?period=all&courierId={courier.Id}")).JsonAsync();
        summary.GetProperty("totals").GetProperty("confirmedCount").GetInt32().ShouldBe(1);
        summary.GetProperty("totals").GetProperty("variance").GetInt64().ShouldBe(-500);
        summary.GetProperty("courierId").GetString().ShouldBe(courier.Id);
        var pdf = await world.Admin.GetAsync($"/api/remittances/{code}/pdf");
        pdf.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        (await world.Admin.GetAsync($"/api/remittances/{code}/export")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Manual_remittance_needs_parcels_and_refuses_a_second_open_cash()
    {
        var (world, courier, phone, parcel) = await DeliveredAsync(factory);
        var open = (await (await phone.GetAsync("/api/courier/me/remittance")).JsonAsync()).GetProperty("remittance").GetProperty("code").GetString()!;

        var empty = await world.Admin.PostAsJsonAsync("/api/remittances", new { courierId = courier.Id, cityId = world.City.Id, agencyId = world.Agency.Id });
        (await empty.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("remittance-needs-parcels");
        var second = await world.Admin.PostAsJsonAsync("/api/remittances", new { courierId = courier.Id, cityId = world.City.Id, agencyId = world.Agency.Id, parcelIds = new[] { parcel.Id } });
        second.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await second.JsonAsync()).GetProperty("details").GetProperty("code").GetString().ShouldBe(open);

        var candidates = await (await world.Admin.GetAsync($"/api/remittances/candidates?courierId={courier.Id}")).JsonAsync();
        candidates.EnumerateArray().Select(c => c.GetProperty("id").GetString()).ShouldContain(parcel.Id);
        (await world.Admin.GetAsync("/api/remittances/candidates")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var removed = await (await world.Admin.DeleteAsync($"/api/remittances/{open}/parcels/{parcel.Id}")).JsonAsync();
        removed.GetProperty("expectedCod").GetInt64().ShouldBe(0);
        removed.GetProperty("confirmable").GetBoolean().ShouldBeFalse();
        var back = await (await world.Admin.PostAsJsonAsync($"/api/remittances/{open}/parcels", new { parcelIds = new[] { parcel.Id } })).JsonAsync();
        back.GetProperty("expectedCod").GetInt64().ShouldBeGreaterThan(0);
        (await world.Admin.DeleteAsync($"/api/remittances/{open}")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Le livreur B ne voit pas la caisse du livreur A.
        var (_, otherPhone) = await RoutesContractTests.CourierAsync(factory, world.City.Id);
        (await (await otherPhone.GetAsync("/api/courier/me/remittance")).JsonAsync()).GetProperty("remittance").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        (await (await otherPhone.GetAsync("/api/remittances")).JsonAsync()).GetProperty("total").GetInt32().ShouldBe(0);
    }
}
