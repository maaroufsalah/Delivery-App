using System.Net;
using System.Net.Http.Json;
using Delivery.ContractTests.Infrastructure;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;

namespace Delivery.ContractTests.Routes;

/// <summary>Balayage de portée du module « Bons et circuits » : chacun ne voit que ce qui lui revient.</summary>
[Collection(ApiCollection.Name)]
public sealed class RoutesScopeSweepContractTests(ApiFactory factory)
{
    [Fact]
    public async Task Notes_routes_courier_space_and_stock_stay_within_their_owner()
    {
        var a = await DeliveryNotesContractTests.WorldAsync(factory);
        var b = await DeliveryNotesContractTests.WorldAsync(factory);
        var (courierA, phoneA) = await RoutesContractTests.CourierAsync(factory, a.City.Id);
        var (courierB, phoneB) = await RoutesContractTests.CourierAsync(factory, b.City.Id);
        await factory.ExecuteDbAsync(async db =>
        {
            foreach (var (courier, agency, city) in new[] { (courierA, a.Agency, a.City), (courierB, b.Agency, b.City) })
            {
                db.Couriers.Attach(courier);
                courier.AgencyId = agency.Id;
                db.CourierCities.Add(new CourierCity { CourierId = courier.Id, CityId = city.Id, DeliveryFee = 15m });
            }
            await db.SaveChangesAsync();
        });

        // Bons : chaque vendeur ne voit que les siens.
        var parcelA = await DeliveryNotesContractTests.ParcelAsync(a);
        var parcelB = await DeliveryNotesContractTests.ParcelAsync(b);
        var noteA = (await (await a.Seller.PostAsJsonAsync("/api/merchant/me/pickups", new { parcelIds = new[] { parcelA.Id } })).JsonAsync()).GetProperty("code").GetString()!;
        (await a.Seller.PostAsJsonAsync("/api/merchant/me/pickups", new { parcelIds = new[] { parcelB.Id } })).StatusCode.ShouldNotBe(HttpStatusCode.OK);
        (await b.Seller.GetAsync($"/api/delivery-notes/{noteA}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await b.Seller.GetAsync($"/api/delivery-notes/{noteA}/pdf")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await (await b.Seller.GetAsync("/api/delivery-notes")).JsonAsync()).GetProperty("total").GetInt32().ShouldBe(0);

        // Circuits : chaque livreur ne voit que les siens, partout où il passe.
        await RoutesContractTests.ReadyAsync(factory, parcelA.Id);
        var routeA = (await (await a.Admin.PostAsJsonAsync("/api/routes", new { cityId = a.City.Id, courierId = courierA.Id })).JsonAsync()).GetProperty("code").GetString()!;
        await a.Admin.PostAsJsonAsync($"/api/routes/{routeA}/parcels", new { parcelIds = new[] { parcelA.Id } });
        (await a.Admin.PostAsync($"/api/routes/{routeA}/dispatch", null)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await phoneA.GetAsync($"/api/routes/{routeA}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        foreach (var path in new[] { $"/api/routes/{routeA}", $"/api/routes/{routeA}/sheet", $"/api/routes/{routeA}/labels", $"/api/courier/me/routes/{routeA}",
                     $"/api/courier/parcels/{parcelA.Code}/outcomes" })
        {
            (await phoneB.GetAsync(path)).StatusCode.ShouldBe(HttpStatusCode.NotFound, path);
        }
        (await phoneB.PostAsJsonAsync($"/api/routes/{routeA}/parcels/{parcelA.Id}/confirm", new { statusId = 4 })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await phoneB.PostAsJsonAsync($"/api/courier/parcels/{parcelA.Code}/status", new { statusId = 4 })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await (await phoneB.GetAsync("/api/routes")).JsonAsync()).GetProperty("total").GetInt32().ShouldBe(0);
        (await (await phoneB.GetAsync("/api/courier/me/deliveries")).JsonAsync()).GetProperty("totalCount").GetInt32().ShouldBe(0);

        // Stock livreur : réservé au personnel ; l'agent ne voit que les livreurs de son agence.
        (await phoneA.GetAsync("/api/courier-stock")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await a.Seller.GetAsync("/api/courier-stock")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var agentB = await TestData.ClientForAsync(factory, await TestData.UserAsync(factory, RoleName.STAFF, b.Agency.Id));
        var stock = await agentB.GetAsync("/api/courier-stock?all=true&pageSize=200");
        if (stock.StatusCode == HttpStatusCode.OK)
        {
            var ids = (await stock.JsonAsync()).GetProperty("items").EnumerateArray().Select(r => r.GetProperty("courierId").GetString()).ToList();
            ids.ShouldNotContain(courierA.Id);
            (await agentB.GetAsync($"/api/courier-stock/{courierA.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }
        else
        {
            stock.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
        var adminStock = await (await a.Admin.GetAsync("/api/courier-stock?all=true&pageSize=200")).JsonAsync();
        adminStock.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("courierId").GetString()).ShouldContain(courierA.Id);
    }
}
