using System.Net;
using Delivery.Application.Common.Interfaces;
using Delivery.ContractTests.Finance;
using Delivery.ContractTests.Infrastructure;
using Delivery.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.ContractTests.Engagement;

/// <summary>`DashboardController` : réservé au personnel, filtré par période, agence et portée.</summary>
[Collection(ApiCollection.Name)]
public sealed class DashboardContractTests(ApiFactory factory)
{
    private static readonly string[] Routes =
        ["", "/series?range=7d", "/couriers", "/cities", "/clients?rank=cod", "/finance", "/routes", "/delays", "/alerts"];

    [Fact]
    public async Task Staff_reads_every_panel_and_merchants_or_couriers_are_refused()
    {
        var (world, courier, phone, _) = await RemittancesContractTests.DeliveredAsync(factory);
        var agency = $"agencyId={world.Agency.Id}";
        foreach (var path in Routes)
        {
            var response = await world.Admin.GetAsync($"/api/dashboard{path}{(path.Contains('?') ? "&" : "?")}{agency}");
            response.StatusCode.ShouldBe(HttpStatusCode.OK, path);
            (await world.Seller.GetAsync($"/api/dashboard{path}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden, path);
            (await phone.GetAsync($"/api/dashboard{path}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden, path);
        }

        var overview = await (await world.Admin.GetAsync($"/api/dashboard?period=today&{agency}")).JsonAsync();
        overview.GetProperty("kpis").GetProperty("delivered").GetInt32().ShouldBe(1);
        overview.GetProperty("window").GetProperty("period").GetString().ShouldBe("today");
        var couriers = await (await world.Admin.GetAsync($"/api/dashboard/couriers?period=today&{agency}")).JsonAsync();
        couriers.GetProperty("items").EnumerateArray().Single(c => c.GetProperty("courierId").GetString() == courier.Id).GetProperty("delivered").GetInt32().ShouldBe(1);
        (await world.Admin.GetAsync($"/api/dashboard/clients/{world.Merchant.Id}?period=today")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await world.Admin.GetAsync("/api/dashboard/clients/inconnu")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var today = factory.Services.GetRequiredService<IClock>().Today();
        (await world.Admin.GetAsync($"/api/dashboard?period=custom&from={today}&to={today}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await world.Admin.GetAsync("/api/dashboard?from=24-11-2026")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await world.Admin.GetAsync("/api/dashboard?period=century")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await world.Admin.GetAsync("/api/dashboard/couriers?pageSize=101")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        (await world.Admin.GetAsync("/api/dashboard/export")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var xlsx = await world.Admin.GetAsync($"/api/dashboard/export?section=couriers&{agency}");
        xlsx.StatusCode.ShouldBe(HttpStatusCode.OK);
        xlsx.Content.Headers.ContentType!.MediaType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var csv = await world.Admin.GetAsync($"/api/dashboard/export?section=cities&format=csv&{agency}");
        csv.StatusCode.ShouldBe(HttpStatusCode.OK);
        csv.Content.Headers.ContentDisposition!.FileName!.Trim('"').ShouldEndWith(".csv");
    }

    [Fact]
    public async Task Agent_of_another_agency_does_not_see_the_delivery()
    {
        var (world, _, _, _) = await RemittancesContractTests.DeliveredAsync(factory);
        var other = await TestData.AgencyAsync(factory, world.City.Id);
        var agent = await TestData.ClientForAsync(factory, await TestData.UserAsync(factory, RoleName.STAFF, other.Id));
        var overview = await (await agent.GetAsync("/api/dashboard?period=today")).JsonAsync();
        var couriers = await (await agent.GetAsync("/api/dashboard/couriers?period=today")).JsonAsync();
        var mine = await TestData.ClientForAsync(factory, await TestData.UserAsync(factory, RoleName.STAFF, world.Agency.Id));
        var own = await (await mine.GetAsync("/api/dashboard?period=today")).JsonAsync();

        overview.GetProperty("kpis").GetProperty("delivered").GetInt32().ShouldBe(0);
        couriers.GetProperty("total").GetInt32().ShouldBe(0);
        own.GetProperty("kpis").GetProperty("delivered").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
    }
}
