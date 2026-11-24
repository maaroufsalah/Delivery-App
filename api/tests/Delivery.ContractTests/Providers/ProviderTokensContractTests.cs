using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Delivery.ContractTests.Infrastructure;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;

namespace Delivery.ContractTests.Providers;

[Collection(ApiCollection.Name)]
public sealed class ProviderTokensContractTests(ApiFactory factory)
{
    [Fact]
    public async Task Provider_tokens_are_created_rotated_mapped_and_revoked()
    {
        var (_, city) = await TestData.CityAsync(factory);
        var admin = await TestData.ClientForAsync(factory, await TestData.UserAsync(factory, RoleName.SUPER_ADMIN));
        var user = await TestData.UserAsync(factory, RoleName.COURIER);
        var courier = new Courier { UserId = user.Id, FirstName = "Ali", LastName = "Amana", Phone = "0600000000", CityId = city.Id };
        var noAccount = new Courier { FirstName = "Sans", LastName = "Compte", Phone = "0600000001", CityId = city.Id };
        await factory.ExecuteDbAsync(async db =>
        {
            db.Couriers.AddRange(courier, noAccount);
            await db.SaveChangesAsync();
        });

        (await admin.PostAsJsonAsync("/api/settings/providers", new { name = "Amana", courierId = noAccount.Id })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var created = await admin.PostAsJsonAsync("/api/settings/providers", new { name = " Amana ", courierId = courier.Id });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var secret = await created.JsonAsync();
        var token = secret.GetProperty("token").GetString()!;
        Regex.IsMatch(token, "^[a-f0-9]{64}$").ShouldBeTrue();
        var item = secret.GetProperty("item");
        item.GetProperty("name").GetString().ShouldBe("Amana");
        item.GetProperty("tokenPrefix").GetString().ShouldBe(token[..8]);
        item.GetProperty("mode").GetString().ShouldBe("LOG_ONLY");
        item.GetProperty("courierName").GetString().ShouldBe("Ali Amana");
        item.GetProperty("statusMap").GetProperty("4").GetInt32().ShouldBe(4);
        var id = item.GetProperty("id").GetString();

        var badMap = await admin.PutAsJsonAsync($"/api/settings/providers/{id}/status-map", new { statusMap = new Dictionary<string, object?> { ["1"] = 4, ["9"] = 999 } });
        badMap.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var issues = (await badMap.JsonAsync()).GetProperty("details").GetProperty("issues").EnumerateArray().Select(i => i.GetProperty("message").GetString()).ToList();
        issues.ShouldBe(["unknown-provider-status", "unknown-target-status"]);
        var mapped = await (await admin.PutAsJsonAsync($"/api/settings/providers/{id}/status-map", new { statusMap = new Dictionary<string, object?> { ["9"] = null, ["30"] = 13 } })).JsonAsync();
        mapped.GetProperty("statusMap").GetProperty("9").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        mapped.GetProperty("statusMap").GetProperty("30").GetInt32().ShouldBe(13);

        var active = await (await admin.PatchAsJsonAsync($"/api/settings/providers/{id}/mode", new { mode = "ACTIVE" })).JsonAsync();
        active.GetProperty("mode").GetString().ShouldBe("ACTIVE");
        var rotated = await (await admin.PostAsJsonAsync($"/api/settings/providers/{id}/rotate", new { })).JsonAsync();
        rotated.GetProperty("token").GetString().ShouldNotBe(token);
        rotated.GetProperty("item").GetProperty("previousExpiresAt").GetString().ShouldNotBeNull();
        (await (await admin.GetAsync($"/api/settings/providers/{id}/calls")).JsonAsync()).GetArrayLength().ShouldBe(0);
        (await admin.PostAsJsonAsync($"/api/settings/providers/{id}/calls/nope/resolve", new { })).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var revoked = await (await admin.PostAsJsonAsync($"/api/settings/providers/{id}/revoke", new { })).JsonAsync();
        revoked.GetProperty("revokedAt").GetString().ShouldNotBeNull();
        revoked.GetProperty("previousExpiresAt").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        (await admin.PostAsJsonAsync($"/api/settings/providers/{id}/rotate", new { })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await (await admin.GetAsync("/api/settings/providers")).JsonAsync()).EnumerateArray().Any(p => p.GetProperty("id").GetString() == id).ShouldBeTrue();

        var merchant = await TestData.MerchantAsync(factory, city.Id);
        var seller = await TestData.ClientForAsync(factory, new User { Id = merchant.UserId!, Email = merchant.Email });
        (await seller.GetAsync("/api/settings/providers")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
