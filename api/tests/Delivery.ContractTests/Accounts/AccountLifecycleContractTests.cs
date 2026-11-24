using System.Net;
using System.Net.Http.Json;
using Delivery.ContractTests.Infrastructure;
using Delivery.Domain.Enums;

namespace Delivery.ContractTests.Accounts;

/// <summary>Cycle de vie des comptes : création par l'admin, connexion, mot de passe, (dés)activation, portée par rôle.</summary>
[Collection(ApiCollection.Name)]
public sealed class AccountLifecycleContractTests(ApiFactory factory)
{
    private const string Password = "Password-1";

    private async Task<HttpResponseMessage> LoginAsync(string identifier, string password) =>
        await factory.CreateMobileClient().PostAsJsonAsync("/api/auth/login", new { identifier, password });

    private async Task<(HttpClient Admin, string CityId)> AdminAsync()
    {
        var (_, city) = await TestData.CityAsync(factory);
        return (await TestData.ClientForAsync(factory, await TestData.UserAsync(factory, RoleName.SUPER_ADMIN)), city.Id);
    }

    private async Task<(string CourierId, string UserId)> CourierAsync(HttpClient admin, string cityId, string? email, string? username)
    {
        var courier = await (await admin.PostAsJsonAsync("/api/couriers", new { firstName = "Samir", lastName = "Compte", phone = "0633333333", cityId })).JsonAsync();
        courier.GetProperty("validatedAt").GetString().ShouldNotBeNull();
        var id = courier.GetProperty("id").GetString()!;
        var account = await admin.PostAsJsonAsync($"/api/couriers/{id}/account", new { email, username, firstName = "Samir", lastName = "Compte", roles = new[] { "COURIER" }, password = Password });
        account.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (id, (await account.JsonAsync()).GetProperty("id").GetString()!);
    }

    [Fact]
    public async Task Admin_creates_a_courier_without_email_and_a_merchant_both_born_validated()
    {
        var (admin, cityId) = await AdminAsync();
        var username = TestData.Unique("liv").ToLowerInvariant();
        await CourierAsync(admin, cityId, null, username);

        var me = await LoginAsync(username, Password);
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        var phone = factory.CreateClient();
        ApiFactory.UseBearer(phone, (await me.JsonAsync()).GetProperty("accessToken").GetString()!);
        (await (await phone.GetAsync("/api/auth/me")).JsonAsync()).GetProperty("roles")[0].GetString().ShouldBe("COURIER");

        var other = await (await admin.PostAsJsonAsync("/api/couriers", new { firstName = "Rachid", lastName = "Double", phone = "0644444444", cityId })).JsonAsync();
        var twin = await admin.PostAsJsonAsync($"/api/couriers/{other.GetProperty("id").GetString()}/account",
            new { username, firstName = "Rachid", lastName = "Double", roles = new[] { "COURIER" }, password = Password });
        twin.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var email = TestData.Unique("boutique") + "@wimo.test";
        var client = await (await admin.PostAsJsonAsync("/api/clients", new { name = TestData.Unique("Boutique "), phone = "0612345678", email, cityId })).JsonAsync();
        client.GetProperty("validatedAt").GetString().ShouldNotBeNull();
        (await admin.PostAsJsonAsync($"/api/clients/{client.GetProperty("id").GetString()}/account",
            new { email, firstName = "Nadia", lastName = "Compte", roles = new[] { "MERCHANT" }, password = Password })).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await LoginAsync(email, Password)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Courier_logs_in_with_his_email_or_his_username()
    {
        var (admin, cityId) = await AdminAsync();
        var email = TestData.Unique("livreur") + "@wimo.test";
        var username = TestData.Unique("liv").ToLowerInvariant();
        await CourierAsync(admin, cityId, email, username);
        (await LoginAsync(email, Password)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await LoginAsync(username, Password)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await LoginAsync(username, "Mauvais-1")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Password_change_replaces_the_old_password()
    {
        var (admin, cityId) = await AdminAsync();
        var username = TestData.Unique("liv").ToLowerInvariant();
        await CourierAsync(admin, cityId, null, username);
        var phone = factory.CreateClient();
        ApiFactory.UseBearer(phone, (await (await LoginAsync(username, Password)).JsonAsync()).GetProperty("accessToken").GetString()!);

        (await phone.PostAsJsonAsync("/api/users/me/password", new { currentPassword = "Mauvais-1", password = "Nouveau-123", confirmation = "Nouveau-123" }))
            .IsSuccessStatusCode.ShouldBeFalse();
        (await phone.PostAsJsonAsync("/api/users/me/password", new { currentPassword = Password, password = "Nouveau-123", confirmation = "Nouveau-123" }))
            .IsSuccessStatusCode.ShouldBeTrue();
        (await LoginAsync(username, Password)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await LoginAsync(username, "Nouveau-123")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Deactivated_account_cannot_log_in_until_reactivated()
    {
        var (admin, cityId) = await AdminAsync();
        var username = TestData.Unique("liv").ToLowerInvariant();
        var (_, userId) = await CourierAsync(admin, cityId, null, username);

        (await admin.PatchAsJsonAsync($"/api/users/{userId}/active", new { isActive = false })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await LoginAsync(username, Password)).IsSuccessStatusCode.ShouldBeFalse();
        (await admin.PatchAsJsonAsync($"/api/users/{userId}/active", new { isActive = true })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await LoginAsync(username, Password)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Courier_and_merchant_reach_their_space_and_are_refused_admin_routes()
    {
        var world = await Routes.DeliveryNotesContractTests.WorldAsync(factory);
        var (_, phone) = await Routes.RoutesContractTests.CourierAsync(factory, world.City.Id);

        (await phone.GetAsync("/api/courier/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await world.Seller.GetAsync("/api/merchant/me/summary")).StatusCode.ShouldBe(HttpStatusCode.OK);
        foreach (var path in new[] { "/api/users", "/api/roles", "/api/dashboard", "/api/couriers", "/api/settings/business", "/api/audit" })
        {
            (await phone.GetAsync(path)).StatusCode.ShouldBe(HttpStatusCode.Forbidden, $"livreur {path}");
            (await world.Seller.GetAsync(path)).StatusCode.ShouldBe(HttpStatusCode.Forbidden, $"vendeur {path}");
        }
        (await world.Admin.GetAsync("/api/users")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
