using System.Net;
using Delivery.ContractTests.Infrastructure;
using Delivery.Domain.Enums;

namespace Delivery.ContractTests.Routes;

[Collection(ApiCollection.Name)]
public sealed class StaffAlertsContractTests(ApiFactory factory)
{
    [Fact]
    public async Task Staff_alerts_show_only_the_groups_the_account_may_read()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var alerts = await (await world.Admin.GetAsync("/api/staff/me/alerts")).JsonAsync();
        alerts.GetProperty("stuckInRetry").GetProperty("count").GetInt32().ShouldBeGreaterThanOrEqualTo(0);
        alerts.GetProperty("staleRoutes").GetProperty("items").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Array);
        alerts.TryGetProperty("pendingNotes", out _).ShouldBeTrue();
        alerts.TryGetProperty("pendingRemittances", out _).ShouldBeTrue();
        alerts.TryGetProperty("openClaims", out _).ShouldBeTrue();

        var (_, courier) = await RoutesContractTests.CourierAsync(factory, world.City.Id);
        (await courier.GetAsync("/api/staff/me/alerts")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await world.Seller.GetAsync("/api/staff/me/alerts")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var agent = await TestData.ClientForAsync(factory, await TestData.UserAsync(factory, RoleName.STAFF, world.Agency.Id));
        var scoped = await agent.GetAsync($"/api/staff/me/alerts?agencyId={Guid.NewGuid()}");
        scoped.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await scoped.JsonAsync()).GetProperty("agencyId").GetString().ShouldBe(world.Agency.Id);
    }
}
