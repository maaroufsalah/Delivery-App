using System.Net;
using System.Net.Http.Json;
using Delivery.ContractTests.Infrastructure;
using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Delivery.ContractTests.Routes;

[Collection(ApiCollection.Name)]
public sealed class CourierAppContractTests(ApiFactory factory)
{
    [Fact]
    public async Task Courier_scans_dispatches_reports_once_and_attaches_a_proof()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var (courier, phone) = await RoutesContractTests.CourierAsync(factory, world.City.Id);
        await factory.ExecuteDbAsync(async db =>
        {
            db.CourierCities.Add(new CourierCity { CourierId = courier.Id, CityId = world.City.Id, DeliveryFee = 15m });
            await db.SaveChangesAsync();
        });
        var kept = await DeliveryNotesContractTests.ParcelAsync(world, cod: 120);
        var dropped = await DeliveryNotesContractTests.ParcelAsync(world);
        var open = await DeliveryNotesContractTests.ParcelAsync(world);
        await RoutesContractTests.ReadyAsync(factory, kept.Id);
        await RoutesContractTests.ReadyAsync(factory, dropped.Id);
        await RoutesContractTests.ReadyAsync(factory, open.Id);

        (await phone.PostAsJsonAsync("/api/courier/me/scan", new { code = "XY" })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await phone.PostAsJsonAsync("/api/courier/me/scan", new { code = "ZZZ000000000ZZZ" })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var first = await phone.PostAsJsonAsync("/api/courier/me/scan", new { code = kept.Code.ToLowerInvariant() });
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var scan = await first.JsonAsync();
        scan.GetProperty("routeCreated").GetBoolean().ShouldBeTrue();
        scan.GetProperty("added").GetProperty("codToCollect").GetInt64().ShouldBe(12000);
        var code = scan.GetProperty("route").GetProperty("code").GetString()!;
        scan.GetProperty("route").GetProperty("status").GetString().ShouldBe("draft");
        (await phone.PostAsJsonAsync("/api/courier/me/scan", new { code = kept.Code })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var second = await (await phone.PostAsJsonAsync("/api/courier/me/scan", new { code = dropped.Code })).JsonAsync();
        second.GetProperty("routeCreated").GetBoolean().ShouldBeFalse();
        second.GetProperty("route").GetProperty("parcelCount").GetInt32().ShouldBe(2);

        (await phone.PostAsJsonAsync("/api/courier/me/scan", new { code = open.Code })).StatusCode.ShouldBe(HttpStatusCode.OK);
        var removed = await phone.DeleteAsync($"/api/courier/me/routes/{code}/parcels/{dropped.Id}");
        removed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await removed.JsonAsync()).GetProperty("parcelCount").GetInt32().ShouldBe(2);
        (await (await phone.GetAsync("/api/courier/me/routes?status=draft")).JsonAsync()).GetArrayLength().ShouldBe(1);

        var dispatched = await phone.PostAsync($"/api/courier/me/routes/{code}/dispatch", null);
        dispatched.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await dispatched.JsonAsync()).GetProperty("status").GetString().ShouldBe("active");
        (await (await phone.GetAsync("/api/courier/me/routes")).JsonAsync()).GetArrayLength().ShouldBe(1);
        var deliveries = await (await phone.GetAsync("/api/courier/me/deliveries")).JsonAsync();
        deliveries.GetProperty("totalCount").GetInt32().ShouldBe(2);
        deliveries.GetProperty("parcels")[0].GetProperty("routeCode").GetString().ShouldBe(code);
        var outcomes = await (await phone.GetAsync($"/api/courier/parcels/{kept.Code}/outcomes")).JsonAsync();
        outcomes.EnumerateArray().Select(o => o.GetInt32()).ShouldContain(4);
        (await phone.PostAsJsonAsync($"/api/courier/parcels/{kept.Code}/proof", new { proof = new { name = "p.jpg", mimeType = "image/gif", content = "AA==" } }))
            .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        using var report = new HttpRequestMessage(HttpMethod.Post, $"/api/courier/parcels/{kept.Code}/status") { Content = JsonContent.Create(new { statusId = 4 }) };
        report.Headers.Add("Idempotency-Key", "key-" + Guid.NewGuid().ToString("N"));
        var key = report.Headers.GetValues("Idempotency-Key").Single();
        var reported = await phone.SendAsync(report);
        reported.StatusCode.ShouldBe(HttpStatusCode.OK, await reported.Content.ReadAsStringAsync());
        var route = await reported.JsonAsync();
        route.GetProperty("deliveredCount").GetInt32().ShouldBe(1);
        await factory.ExecuteDbAsync(async db => (await db.IdempotentRequests.CountAsync(r => r.Key == key)).ShouldBe(1));
        using var replay = new HttpRequestMessage(HttpMethod.Post, $"/api/courier/parcels/{kept.Code}/status") { Content = JsonContent.Create(new { statusId = 4 }) };
        replay.Headers.Add("Idempotency-Key", key);
        var again = await phone.SendAsync(replay);
        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await again.JsonAsync()).GetProperty("code").GetString().ShouldBe(code);
        await factory.ExecuteDbAsync(async db =>
        {
            (await db.ParcelEvents.CountAsync(e => e.ParcelId == kept.Id && e.StatusId == 4)).ShouldBe(1);
            (await db.CashRemittanceParcels.CountAsync(l => l.ParcelId == kept.Id)).ShouldBe(1);
        });

        var proof = await phone.PostAsJsonAsync($"/api/courier/parcels/{kept.Code}/proof", new { proof = new { name = "p.jpg", mimeType = "image/jpeg", content = Convert.ToBase64String([0xFF, 0xD8, 0xFF]) } });
        proof.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await proof.JsonAsync()).GetProperty("attached").GetBoolean().ShouldBeTrue();

        var performance = await (await phone.GetAsync("/api/courier/me/performance")).JsonAsync();
        performance.GetProperty("period").GetString().ShouldBe("day");
        performance.GetProperty("collectedInCents").GetInt64().ShouldBe(12000);
        performance.GetProperty("toRemitInCents").GetInt64().ShouldBe(12000);
        performance.GetProperty("deliveryRate").GetProperty("percent").GetInt32().ShouldBe(50);
        (await phone.GetAsync("/api/courier/me/performance?period=year")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await phone.GetAsync("/api/courier/me/notes")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await world.Admin.GetAsync("/api/courier/me/routes")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var (_, stranger) = await RoutesContractTests.CourierAsync(factory, world.City.Id);
        (await stranger.GetAsync($"/api/courier/me/routes/{code}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await stranger.GetAsync($"/api/courier/parcels/{kept.Code}/outcomes")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
