using System.Net;
using System.Net.Http.Json;
using Delivery.ContractTests.Infrastructure;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.ContractTests.Routes;

/// <summary>
/// Recette locale du 27/11/2026 : un colis à retenter (reporté, injoignable, pas de réponse) repart dans le circuit de
/// livraison suivant, comme dans Wimo Laravel ; un circuit de livraison ne se clôt pas à la main ; la fiche de paie du
/// livreur suit la grille de la ville du colis ; un colis rentré puis reparti ne va pas sur un bon de retour.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class RedeliveryContractTests(ApiFactory factory)
{
    [Fact]
    public async Task A_postponed_parcel_leaves_again_in_the_next_delivery_route_which_never_closes_by_hand()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var (courier, phone) = await RoutesContractTests.CourierAsync(factory, world.City.Id);
        await factory.ExecuteDbAsync(async db =>
        {
            db.CourierCities.Add(new CourierCity { CourierId = courier.Id, CityId = world.City.Id, DeliveryFee = 15m });
            await db.SaveChangesAsync();
        });
        var parcel = await DeliveryNotesContractTests.ParcelAsync(world, 120);
        await RoutesContractTests.ReadyAsync(factory, parcel.Id);
        var first = (await (await world.Admin.PostAsJsonAsync("/api/routes", new { cityId = world.City.Id, courierId = courier.Id })).JsonAsync()).GetProperty("code").GetString()!;
        await world.Admin.PostAsJsonAsync($"/api/routes/{first}/parcels", new { parcelIds = new[] { parcel.Id } });
        await world.Admin.PostAsync($"/api/routes/{first}/dispatch", null);
        (await phone.PostAsJsonAsync($"/api/courier/parcels/{parcel.Code}/status", new { statusId = 14, scheduledAt = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd") }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // L'écran ne propose jamais la clôture d'un circuit de livraison : l'API la refuse aussi.
        var detail = await (await world.Admin.GetAsync($"/api/routes/{first}")).JsonAsync();
        detail.GetProperty("closable").GetBoolean().ShouldBeFalse();
        var refused = await world.Admin.PostAsJsonAsync($"/api/routes/{first}/close", new { });
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await refused.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("delivery-route-auto-close");

        // Un circuit déjà clos (avant ce correctif, ou par la clôture automatique) qui garde un colis reporté.
        await factory.ExecuteDbAsync(async db =>
        {
            (await db.Routes.SingleAsync(r => r.Code == first)).Status = RouteStatus.CLOSED;
            await db.SaveChangesAsync();
        });
        var second = (await (await world.Admin.PostAsJsonAsync("/api/routes", new { cityId = world.City.Id, courierId = courier.Id })).JsonAsync()).GetProperty("code").GetString()!;
        var candidates = await (await world.Admin.GetAsync($"/api/routes/{second}/candidates")).JsonAsync();
        candidates.EnumerateArray().Select(c => c.GetProperty("code").GetString()).ShouldContain(parcel.Code);
        (await world.Admin.PostAsJsonAsync($"/api/routes/{second}/parcels", new { parcelIds = new[] { parcel.Id } })).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await world.Admin.PostAsync($"/api/routes/{second}/dispatch", null)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await factory.QueryDbAsync(db => db.Parcels.Where(p => p.Id == parcel.Id).Select(p => p.StatusId).SingleAsync())).ShouldBe(13);
        (await phone.PostAsJsonAsync($"/api/courier/parcels/{parcel.Code}/status", new { statusId = 4, collectedAmount = 120 })).StatusCode.ShouldBe(HttpStatusCode.OK);
        var link = await factory.QueryDbAsync(db => db.RouteParcels.SingleAsync(l => l.ParcelId == parcel.Id && l.Route.Code == second));
        link.CodCollected.ShouldBe(120m);
        link.CourierCommission.ShouldBe(15m);
    }

    [Fact]
    public async Task The_courier_payslip_pays_pickups_per_parcel_at_the_pickup_city_rate_without_any_other_city()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var (_, other) = await TestData.CityAsync(factory, "Ville Hors Grille");
        var (courier, _) = await RoutesContractTests.CourierAsync(factory, world.City.Id);
        var inGrid = await DeliveryNotesContractTests.ParcelAsync(world, 100);
        var outOfGrid = await DeliveryNotesContractTests.ParcelAsync(world, 100);
        await factory.ExecuteDbAsync(async db =>
        {
            db.CourierCities.Add(new CourierCity { CourierId = courier.Id, CityId = world.City.Id, PickupFee = 5m, DeliveryFee = 15m });
            (await db.Parcels.SingleAsync(p => p.Id == outOfGrid.Id)).OriginCityId = other.Id;
            var route = new Route
            {
                Code = $"CIR-T{Cuid.New()[^8..]}", Status = RouteStatus.CLOSED, StatusAt = DateTime.UtcNow, Kind = RouteKind.PICKUP, CityId = world.City.Id, OriginCityId = world.City.Id,
                CourierId = courier.Id, CreatedAt = DateTime.UtcNow,
            };
            db.Routes.Add(route);
            foreach (var id in new[] { inGrid.Id, outOfGrid.Id })
            {
                db.RouteParcels.Add(new RouteParcel { RouteId = route.Id, ParcelId = id, Confirmed = true, ConfirmedAt = DateTime.UtcNow, FinalStatusId = 18 });
            }
            await db.SaveChangesAsync();
        });
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var statement = await (await world.Admin.PostAsJsonAsync("/api/courier-statements", new { courierId = courier.Id, agencyId = world.Agency.Id, periodStart = today, periodEnd = today }))
            .JsonAsync();
        var lines = statement.GetProperty("lines").EnumerateArray().ToList();
        // Le colis dont la ville de ramassage est sur la grille : 5,00 ; l'autre : 0 (Laravel : « frais = 0 »), donc aucune ligne.
        lines.Select(l => (l.GetProperty("parcelCode").GetString(), l.GetProperty("kind").GetString(), l.GetProperty("amount").GetInt64())).ShouldBe([(inGrid.Code, "pickup", 500L)]);
        statement.GetProperty("commissionsTotal").GetInt64().ShouldBe(500);
        statement.GetProperty("totalDue").GetInt64().ShouldBe(500);
    }

    [Fact]
    public async Task A_parcel_received_back_then_sent_out_again_is_neither_proposed_nor_accepted_on_a_return_slip()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var (courier, _) = await RoutesContractTests.CourierAsync(factory, world.City.Id);
        var resent = await DeliveryNotesContractTests.ParcelAsync(world, 100);
        var returned = await DeliveryNotesContractTests.ParcelAsync(world, 100);
        await factory.ExecuteDbAsync(async db =>
        {
            var handover = new ReturnHandover
            {
                Code = $"RL-T{Cuid.New()[^8..]}", Status = ReturnHandoverStatus.CONFIRMED, CourierId = courier.Id, AgencyId = world.Agency.Id, ConfirmedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            };
            db.ReturnHandovers.Add(handover);
            foreach (var (id, status) in new[] { (resent.Id, 4), (returned.Id, 22) })
            {
                db.ReturnHandoverItems.Add(new ReturnHandoverItem { HandoverId = handover.Id, ParcelId = id, CreatedAt = DateTime.UtcNow });
                // Rentré (ligne non corrigée), puis reparti et livré pour le premier : la ligne dit encore « rentré ».
                (await db.Parcels.SingleAsync(p => p.Id == id)).StatusId = status;
            }
            await db.SaveChangesAsync();
        });
        var slip = (await (await world.Admin.PostAsJsonAsync("/api/return-slips", new { clientId = world.Merchant.Id, agencyId = world.Agency.Id })).JsonAsync()).GetProperty("code").GetString()!;
        var candidates = await (await world.Admin.GetAsync($"/api/return-slips/{slip}/candidates")).JsonAsync();
        var codes = candidates.EnumerateArray().Select(c => c.GetProperty("code").GetString()).ToList();
        codes.ShouldContain(returned.Code);
        codes.ShouldNotContain(resent.Code);

        var refused = await world.Admin.PostAsJsonAsync($"/api/return-slips/{slip}/parcels", new { parcelId = resent.Id, reason = "refused" });
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await refused.JsonAsync();
        body.GetProperty("details").GetProperty("reason").GetString().ShouldBe("parcel-not-returnable");
        body.GetProperty("details").GetProperty("statusId").GetInt32().ShouldBe(4);
        (await world.Admin.PostAsJsonAsync($"/api/return-slips/{slip}/parcels", new { parcelId = returned.Id, reason = "refused" })).StatusCode.ShouldBe(HttpStatusCode.Created);
    }
}
