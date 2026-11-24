using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Delivery.ContractTests.Infrastructure;
using Delivery.ContractTests.Routes;
using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Delivery.ContractTests.Providers;

[Collection(ApiCollection.Name)]
public sealed class ProviderStatusContractTests(ApiFactory factory)
{
    private static HttpRequestMessage Report(string code, string token, string? key, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/provider/parcels/{code}/status") { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return request;
    }

    [Fact]
    public async Task Provider_reports_are_journaled_replayed_and_applied_through_the_courier_route()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var (courier, _) = await RoutesContractTests.CourierAsync(factory, world.City.Id);
        await factory.ExecuteDbAsync(async db =>
        {
            db.CourierCities.Add(new CourierCity { CourierId = courier.Id, CityId = world.City.Id, DeliveryFee = 15m });
            await db.SaveChangesAsync();
        });
        var refused = await DeliveryNotesContractTests.ParcelAsync(world);
        var delivered = await DeliveryNotesContractTests.ParcelAsync(world, cod: 99.5);
        await RoutesContractTests.ReadyAsync(factory, refused.Id);
        await RoutesContractTests.ReadyAsync(factory, delivered.Id);
        var code = (await (await world.Admin.PostAsJsonAsync("/api/routes", new { cityId = world.City.Id, courierId = courier.Id })).JsonAsync()).GetProperty("code").GetString()!;
        await world.Admin.PostAsJsonAsync($"/api/routes/{code}/parcels", new { parcelIds = new[] { refused.Id, delivered.Id } });
        (await world.Admin.PostAsync($"/api/routes/{code}/dispatch", null)).StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await (await world.Admin.PostAsJsonAsync("/api/settings/providers", new { name = "Amana", courierId = courier.Id })).JsonAsync();
        var token = created.GetProperty("token").GetString()!;
        var id = created.GetProperty("item").GetProperty("id").GetString();
        var client = factory.CreateClient();
        var at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        (await client.SendAsync(Report(refused.Code, new string('0', 64), "k-1", new { providerStatus = 8, occurredAt = at }))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.SendAsync(Report(refused.Code, token, null, new { providerStatus = 8, occurredAt = at }))).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await client.SendAsync(Report(refused.Code, token, "k-1", new { providerStatus = 8, occurredAt = "hier" }))).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var logOnly = await (await client.SendAsync(Report(refused.Code, token, "k-1", new { providerStatus = 8, occurredAt = at }))).JsonAsync();
        logOnly.GetProperty("outcome").GetString().ShouldBe("WOULD_APPLY");
        logOnly.GetProperty("mode").GetString().ShouldBe("LOG_ONLY");
        logOnly.GetProperty("statusId").GetInt32().ShouldBe(13);
        var replayed = await (await client.SendAsync(Report(refused.Code, token, "k-1", new { providerStatus = 8, occurredAt = at }))).JsonAsync();
        replayed.GetProperty("outcome").GetString().ShouldBe("WOULD_APPLY");
        replayed.GetProperty("reason").GetString().ShouldBe("replayed");
        (await client.SendAsync(Report(delivered.Code, token, "k-1", new { providerStatus = 8, occurredAt = at }))).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        (await world.Admin.PatchAsJsonAsync($"/api/settings/providers/{id}/mode", new { mode = "ACTIVE" })).StatusCode.ShouldBe(HttpStatusCode.OK);
        var applied = await (await client.SendAsync(Report(refused.Code, token, "k-2", new { providerStatus = 8, occurredAt = at }))).JsonAsync();
        applied.GetProperty("outcome").GetString().ShouldBe("APPLIED");
        applied.GetProperty("statusId").GetInt32().ShouldBe(8);
        var again = await (await client.SendAsync(Report(refused.Code, token, "k-3", new { providerStatus = 8, occurredAt = at }))).JsonAsync();
        again.GetProperty("outcome").GetString().ShouldBe("ALREADY_APPLIED");

        (await client.SendAsync(Report(delivered.Code, token, "k-4", new { providerStatus = 4, occurredAt = at }))).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await client.SendAsync(Report(delivered.Code, token, "k-5", new { providerStatus = 4, occurredAt = at, collectedAmount = 10 }))).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var paid = await (await client.SendAsync(Report(delivered.Code, token, "k-6", new { providerStatus = 4, occurredAt = at, collectedAmount = 99.5 }))).JsonAsync();
        paid.GetProperty("outcome").GetString().ShouldBe("APPLIED");
        await factory.ExecuteDbAsync(async db =>
        {
            (await db.RouteParcels.AsNoTracking().SingleAsync(l => l.ParcelId == delivered.Id)).CodCollected.ShouldBe(99.5m);
            (await db.ProviderCalls.CountAsync(c => c.Token.CourierId == courier.Id)).ShouldBeGreaterThanOrEqualTo(8);
            (await db.ProviderCalls.AnyAsync(c => c.Token.CourierId == courier.Id && c.Reason == "collected-amount-required" && c.Alert)).ShouldBeTrue();
        });
    }
}
