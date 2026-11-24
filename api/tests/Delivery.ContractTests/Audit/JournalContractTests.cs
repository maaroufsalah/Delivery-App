using System.Net;
using System.Net.Http.Json;
using Delivery.ContractTests.Infrastructure;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;

namespace Delivery.ContractTests.Audit;

[Collection(ApiCollection.Name)]
public sealed class JournalContractTests(ApiFactory factory)
{
    [Fact]
    public async Task Journal_groups_writes_by_request_and_reads_them_in_words()
    {
        var (_, city) = await TestData.CityAsync(factory);
        await TestData.AgencyAsync(factory, city.Id);
        var merchant = await TestData.MerchantAsync(factory, city.Id);
        var admin = await TestData.ClientForAsync(factory, await TestData.UserAsync(factory, RoleName.SUPER_ADMIN));
        var created = await (await admin.PostAsJsonAsync("/api/parcels", new
        {
            clientId = merchant.Id, description = "Montre", recipientName = "Sara", recipientPhone = "0611111111", originCityId = city.Id,
            pickupAddress = "A", destinationCityId = city.Id, deliveryAddress = "B", codAmount = 300,
        })).JsonAsync();
        var code = created.GetProperty("code").GetString();
        var parcelId = created.GetProperty("id").GetString();
        await admin.PatchAsJsonAsync($"/api/parcels/{code}/fees", new { deliveryFee = 35, reason = "Hors gabarit" });
        await admin.PostAsJsonAsync($"/api/parcels/{code}/events", new { statusId = 3 });

        var journal = await admin.GetAsync($"/api/audit/journal/entity/Parcel/{parcelId}");
        journal.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await journal.JsonAsync();
        page.GetProperty("total").GetInt32().ShouldBe(3);
        var items = page.GetProperty("items").EnumerateArray().ToList();
        items.Select(i => i.GetProperty("action").GetString()).ShouldBe(["status", "amount", "create"]);
        items[0].GetProperty("headline").GetProperty("after").GetProperty("kind").GetString().ShouldBe("parcel-status");
        items[0].GetProperty("object").GetProperty("type").GetString().ShouldBe("parcel");
        items[0].GetProperty("object").GetProperty("href").GetString().ShouldBe($"/parcels/{code}");
        items[1].GetProperty("headline").GetProperty("after").GetProperty("cents").GetInt64().ShouldBe(3500);
        items[2].GetProperty("user").GetProperty("role").GetString().ShouldBe("admin");
        items[2].TryGetProperty("changes", out _).ShouldBeFalse();

        var entry = await (await admin.GetAsync($"/api/audit/journal/entry/{items[1].GetProperty("id").GetString()}")).JsonAsync();
        entry.GetProperty("reason").GetString().ShouldBe("Hors gabarit");
        entry.GetProperty("changes").GetArrayLength().ShouldBeGreaterThan(0);

        (await admin.GetAsync("/api/audit/journal?action=amount&category=parcels")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admin.GetAsync("/api/audit/journal?view=oops")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var export = await admin.GetAsync($"/api/audit/journal/export?search={code}");
        export.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admin.GetAsync("/api/audit")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var seller = await TestData.ClientForAsync(factory, new User { Id = merchant.UserId!, Email = merchant.Email });
        (await seller.GetAsync("/api/audit/journal")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
