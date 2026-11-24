using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Delivery.ContractTests.Infrastructure;
using Delivery.Domain.Enums;

namespace Delivery.ContractTests.Referential;

[Collection(ApiCollection.Name)]
public sealed class ReferentialContractTests(ApiFactory factory)
{
    private async Task<HttpClient> AdminAsync() => await TestData.ClientForAsync(factory, await TestData.UserAsync(factory, RoleName.SUPER_ADMIN));

    private static string Code()
    {
        const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        return string.Concat(Enumerable.Range(0, 3).Select(_ => letters[Random.Shared.Next(letters.Length)]));
    }

    [Fact]
    public async Task City_lifecycle_follows_speedy()
    {
        var admin = await AdminAsync();
        var (zone, _) = await TestData.CityAsync(factory);
        var code = Code();

        var created = await admin.PostAsJsonAsync("/api/cities", new { name = "  Settat ", code = code.ToLowerInvariant(), zoneId = zone.Id });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var city = await created.JsonAsync();
        city.GetProperty("code").GetString().ShouldBe(code);
        city.GetProperty("name").GetString().ShouldBe("Settat");
        city.GetProperty("nameAr").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        city.GetProperty("zoneName").GetString().ShouldBe(zone.Name);
        city.GetProperty("codeLocked").GetBoolean().ShouldBeFalse();
        var id = city.GetProperty("id").GetString();

        var duplicate = await admin.PostAsJsonAsync("/api/cities", new { name = "Autre", code, zoneId = zone.Id });
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await duplicate.JsonAsync()).GetProperty("details").GetProperty("field").GetString().ShouldBe("code");

        var invalid = await admin.PostAsJsonAsync("/api/cities", new { name = "X", code = "1", zoneId = "" });
        invalid.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var messages = (await invalid.JsonAsync()).GetProperty("details").GetProperty("issues").EnumerateArray()
            .Select(i => $"{i.GetProperty("path").GetString()}:{i.GetProperty("message").GetString()}").ToList();
        messages.ShouldBe(["name:too-short", "code:invalid-city-code", "zoneId:required"]);

        var list = await (await admin.GetAsync($"/api/cities?zoneId={zone.Id}&sort=name")).JsonAsync();
        list.GetProperty("total").GetInt32().ShouldBe(2);

        (await admin.PostAsJsonAsync("/api/districts", new { cityId = id, name = "Centre" })).StatusCode.ShouldBe(HttpStatusCode.Created);
        var twin = await admin.PostAsJsonAsync("/api/districts", new { cityId = id, name = "CENTRE" });
        twin.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var blocked = await admin.DeleteAsync($"/api/cities/{id}");
        blocked.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var references = (await blocked.JsonAsync()).GetProperty("details").GetProperty("references");
        references[0].GetProperty("entity").GetString().ShouldBe("districts");
        references[0].GetProperty("count").GetInt32().ShouldBe(1);

        var inactive = await (await admin.PatchAsJsonAsync($"/api/cities/{id}/active", new { isActive = false })).JsonAsync();
        inactive.GetProperty("isActive").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Zones_store_mad_and_answer_cents()
    {
        var admin = await AdminAsync();
        var a = await (await admin.PostAsJsonAsync("/api/zones", new { name = TestData.Unique("Zone A"), baseRate = 35.5, sameCityRate = 20 })).JsonAsync();
        a.GetProperty("baseRate").GetInt64().ShouldBe(3550);
        a.GetProperty("deliveryDays").GetInt32().ShouldBe(2);
        a.GetProperty("cityIds").GetArrayLength().ShouldBe(0);
        var b = await (await admin.PostAsJsonAsync("/api/zones", new { name = TestData.Unique("Zone B"), baseRate = 50, sameCityRate = 25 })).JsonAsync();

        var rate = await admin.PostAsJsonAsync("/api/zones/rates", new { fromZoneId = a.GetProperty("id").GetString(), toZoneId = b.GetProperty("id").GetString(), rate = 42.25 });
        rate.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await rate.JsonAsync()).GetProperty("rate").GetInt64().ShouldBe(4225);

        var self = await admin.PostAsJsonAsync("/api/zones/rates", new { fromZoneId = a.GetProperty("id").GetString(), toZoneId = a.GetProperty("id").GetString(), rate = 1 });
        self.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var negative = await admin.PostAsJsonAsync("/api/zones", new { name = "Zone C", baseRate = -1, sameCityRate = 10, deliveryDays = 1.5 });
        var issues = (await negative.JsonAsync()).GetProperty("details").GetProperty("issues").EnumerateArray().Select(i => i.GetProperty("message").GetString()).ToList();
        issues.ShouldBe(["negative-amount", "Invalid input: expected int, received number"]);

        var matrix = await (await admin.GetAsync("/api/zones/rates")).JsonAsync();
        matrix.GetProperty("rates").EnumerateArray().ShouldContain(r => r.GetProperty("rate").GetInt64() == 4225);
    }

    [Fact]
    public async Task Default_delivery_type_is_protected()
    {
        var admin = await AdminAsync();
        var list = await (await admin.GetAsync("/api/delivery-types")).JsonAsync();
        var standard = list.GetProperty("items").EnumerateArray().First(t => t.GetProperty("isDefault").GetBoolean());
        standard.GetProperty("multiplierPerMille").GetInt64().ShouldBe(1000);
        var id = standard.GetProperty("id").GetString();

        var deactivate = await admin.PatchAsJsonAsync($"/api/delivery-types/{id}/active", new { isActive = false });
        deactivate.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await admin.DeleteAsync($"/api/delivery-types/{id}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var express = await admin.PostAsJsonAsync("/api/delivery-types", new { name = TestData.Unique("Express "), multiplierPerMille = 1500 });
        (await express.JsonAsync()).GetProperty("multiplierPerMille").GetInt64().ShouldBe(1500);
    }

    [Fact]
    public async Task Headquarters_cannot_be_removed_and_agents_see_their_agency_only()
    {
        var admin = await AdminAsync();
        var agencies = await (await admin.GetAsync("/api/agencies?pageSize=200&sort=createdAt")).JsonAsync();
        var headquarters = agencies.GetProperty("items").EnumerateArray().Single(a => a.GetProperty("isHeadquarters").GetBoolean());
        (await admin.DeleteAsync($"/api/agencies/{headquarters.GetProperty("id").GetString()}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var (_, city) = await TestData.CityAsync(factory);
        var agency = await TestData.AgencyAsync(factory, city.Id);
        var agent = await TestData.UserAsync(factory, RoleName.STAFF, agency.Id);
        await factory.ExecuteDbAsync(async db =>
        {
            db.UserPermissionOverrides.Add(new Domain.Entities.UserPermissionOverride { UserId = agent.Id, PermissionId = "accounts.read", Granted = true });
            await db.SaveChangesAsync();
        });
        var mine = await (await (await TestData.ClientForAsync(factory, agent)).GetAsync("/api/agencies")).JsonAsync();
        mine.GetProperty("total").GetInt32().ShouldBe(1);
        mine.GetProperty("items")[0].GetProperty("id").GetString().ShouldBe(agency.Id);
    }

    [Fact]
    public async Task Pricing_grid_by_origin_round_trips_through_xlsx()
    {
        var admin = await AdminAsync();
        var (zone, origin) = await TestData.CityAsync(factory, "Origine");
        var destination = await (await admin.PostAsJsonAsync("/api/cities", new { name = "Destination", code = Code(), zoneId = zone.Id })).JsonAsync();
        var destinationId = destination.GetProperty("id").GetString()!;

        var rule = await admin.PostAsJsonAsync("/api/pricing-rules", new { originCityId = origin.Id, destinationCityId = destinationId, rateDistrict = 25, rateAgency = 22.5, rateRelayPoint = 22.5 });
        rule.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await rule.JsonAsync()).GetProperty("rateAgency").GetInt64().ShouldBe(2250);
        (await admin.PostAsJsonAsync("/api/pricing-rules", new { originCityId = origin.Id, destinationCityId = destinationId, rateDistrict = 1, rateAgency = 1, rateRelayPoint = 1 }))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var page = await (await admin.GetAsync($"/api/pricing-rules/by-origin?originCityId={origin.Id}&search=destination")).JsonAsync();
        page.GetProperty("origin").GetProperty("id").GetString().ShouldBe(origin.Id);
        page.GetProperty("items")[0].GetProperty("rate").GetInt64().ShouldBe(2500);
        page.GetProperty("items")[0].GetProperty("zoneRate").GetInt64().ShouldBe(3000);

        var saved = await (await admin.PutAsJsonAsync($"/api/pricing-rules/by-origin/{origin.Id}", new { rows = new object[] { new { destinationCityId = destinationId, rate = 30, deliveryDelay = "24h" } } })).JsonAsync();
        saved.GetProperty("priced").GetInt32().ShouldBe(1);
        saved.GetProperty("delays").GetInt32().ShouldBe(1);

        var missingRate = await admin.PutAsJsonAsync($"/api/pricing-rules/by-origin/{origin.Id}", new { rows = new object[] { new { destinationCityId = destinationId } } });
        missingRate.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var export = await admin.GetAsync($"/api/pricing-rules/by-origin/{origin.Id}/export");
        export.Content.Headers.ContentDisposition!.ToString().ShouldBe($"attachment; filename=\"tarifs-{origin.Code}.xlsx\"");

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Tarifs");
        sheet.Cell(1, 1).Value = "Ville";
        sheet.Cell(1, 2).Value = "Tarif";
        sheet.Cell(2, 1).Value = "destination";
        sheet.Cell(2, 2).Value = "35 dh";
        sheet.Cell(3, 1).Value = "Atlantide";
        sheet.Cell(3, 2).Value = "10";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var preview = await (await admin.PostAsJsonAsync($"/api/pricing-rules/by-origin/{origin.Id}/import", new { content = Convert.ToBase64String(stream.ToArray()), apply = true })).JsonAsync();
        preview.GetProperty("changes").GetInt32().ShouldBe(1);
        preview.GetProperty("recognized")[0].GetProperty("rate").GetInt64().ShouldBe(3500);
        preview.GetProperty("rejected")[0].GetProperty("code").GetString().ShouldBe("unknown-city");
    }

    [Fact]
    public async Task Scope_sweep_merchants_and_couriers_never_read_the_internal_grid()
    {
        var (_, city) = await TestData.CityAsync(factory);
        var merchant = await TestData.MerchantAsync(factory, city.Id);
        var merchantClient = await TestData.ClientForAsync(factory, await factory.QueryDbAsync(db => Task.FromResult(db.Users.Single(u => u.Id == merchant.UserId))));
        var courier = await TestData.UserAsync(factory, RoleName.COURIER);
        await factory.ExecuteDbAsync(async db =>
        {
            db.Couriers.Add(new Domain.Entities.Courier { UserId = courier.Id, FirstName = "Ali", LastName = "B", Phone = "0600000001", CityId = city.Id });
            await db.SaveChangesAsync();
        });
        var courierClient = await TestData.ClientForAsync(factory, courier);

        foreach (var route in new[] { "/api/cities", "/api/zones", "/api/districts", "/api/pricing-rules", "/api/delivery-types" })
        {
            var refused = await merchantClient.GetAsync(route);
            refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden, route);
        }
        (await courierClient.GetAsync("/api/zones")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var zones = await (await merchantClient.GetAsync("/api/directory/zones")).JsonAsync();
        zones.EnumerateArray().Any(z => z.TryGetProperty("baseRate", out _)).ShouldBeFalse();
        (await (await merchantClient.GetAsync("/api/directory/couriers")).JsonAsync()).GetArrayLength().ShouldBe(0);
        var clients = await (await merchantClient.GetAsync("/api/directory/clients")).JsonAsync();
        clients.GetArrayLength().ShouldBe(1);
        clients[0].GetProperty("type").GetString().ShouldBe("merchant");
    }
}
