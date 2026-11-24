using System.Net;
using System.Net.Http.Json;
using Delivery.ContractTests.Infrastructure;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.ContractTests.Routes;

[Collection(ApiCollection.Name)]
public sealed class RoutesContractTests(ApiFactory factory)
{
    internal static async Task<(Courier Courier, HttpClient Client)> CourierAsync(ApiFactory factory, string cityId)
    {
        var user = await TestData.UserAsync(factory, RoleName.COURIER);
        var courier = new Courier { UserId = user.Id, FirstName = "Hamid", LastName = "Circuit", Phone = "0600000019", CityId = cityId };
        await factory.ExecuteDbAsync(async db =>
        {
            db.Couriers.Add(courier);
            await db.SaveChangesAsync();
        });
        return (courier, await TestData.ClientForAsync(factory, user));
    }

    internal static Task ReadyAsync(ApiFactory factory, string parcelId) =>
        factory.ExecuteDbAsync(async db =>
        {
            var parcel = await db.Parcels.SingleAsync(p => p.Id == parcelId);
            parcel.StatusId = 3;
            await db.SaveChangesAsync();
        });

    [Fact]
    public async Task Draft_route_is_created_filled_emptied_and_deleted_within_courier_scope()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var (courier, courierClient) = await CourierAsync(factory, world.City.Id);
        var parcel = await DeliveryNotesContractTests.ParcelAsync(world);
        await ReadyAsync(factory, parcel.Id);

        (await world.Admin.PostAsJsonAsync("/api/routes", new { courierId = courier.Id })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var created = await world.Admin.PostAsJsonAsync("/api/routes", new { cityId = world.City.Id, courierId = courier.Id });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var route = await created.JsonAsync();
        var code = route.GetProperty("code").GetString()!;
        code.ShouldStartWith("CIR-");
        route.GetProperty("status").GetString().ShouldBe("draft");
        route.GetProperty("kind").GetString().ShouldBe("delivery");

        var candidates = await (await world.Admin.GetAsync($"/api/routes/{code}/candidates")).JsonAsync();
        candidates.EnumerateArray().Select(c => c.GetProperty("id").GetString()).ShouldContain(parcel.Id);
        var page = await (await world.Admin.GetAsync($"/api/routes/{code}/candidates?page=1&pageSize=5")).JsonAsync();
        page.GetProperty("total").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
        page.GetProperty("cities").GetArrayLength().ShouldBeGreaterThanOrEqualTo(1);
        (await (await world.Admin.GetAsync($"/api/routes/{code}/note-candidates")).JsonAsync()).GetArrayLength().ShouldBe(0);

        var added = await world.Admin.PostAsJsonAsync($"/api/routes/{code}/parcels", new { parcelIds = new[] { parcel.Id } });
        added.StatusCode.ShouldBe(HttpStatusCode.Created);
        var detail = await added.JsonAsync();
        detail.GetProperty("parcels").GetArrayLength().ShouldBe(1);
        detail.GetProperty("parcels")[0].GetProperty("code").GetString().ShouldBe(parcel.Code);

        (await world.Admin.PatchAsJsonAsync($"/api/routes/{code}", new { kind = "pickup" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await world.Admin.PatchAsJsonAsync($"/api/routes/{code}", new { })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await world.Admin.DeleteAsync($"/api/routes/{code}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await courierClient.GetAsync($"/api/routes/{code}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        var list = await (await courierClient.GetAsync("/api/routes")).JsonAsync();
        list.GetProperty("total").GetInt32().ShouldBe(1);
        var stats = await (await world.Admin.GetAsync($"/api/routes/stats?courierId={courier.Id}")).JsonAsync();
        stats.GetProperty("total").GetInt32().ShouldBe(1);
        stats.GetProperty("parcels").GetInt32().ShouldBe(1);
        (await world.Admin.GetAsync("/api/routes/export")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await courierClient.GetAsync($"/api/routes/{code}/export")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var (_, stranger) = await CourierAsync(factory, world.City.Id);
        (await stranger.GetAsync($"/api/routes/{code}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await (await stranger.GetAsync("/api/routes")).JsonAsync()).GetProperty("total").GetInt32().ShouldBe(0);

        var removed = await world.Admin.DeleteAsync($"/api/routes/{code}/parcels/{parcel.Id}");
        removed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await removed.JsonAsync()).GetProperty("parcels").GetArrayLength().ShouldBe(0);

        var patched = await world.Admin.PatchAsJsonAsync($"/api/routes/{code}", new { kind = "pickup" });
        patched.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await patched.JsonAsync()).GetProperty("kind").GetString().ShouldBe("pickup");

        var deleted = await world.Admin.DeleteAsync($"/api/routes/{code}");
        deleted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await deleted.JsonAsync()).GetProperty("code").GetString().ShouldBe(code);
        (await world.Admin.GetAsync($"/api/routes/{code}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
