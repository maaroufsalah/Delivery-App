using System.Net;
using System.Net.Http.Json;
using Delivery.ContractTests.Infrastructure;
using Delivery.Domain.Enums;

namespace Delivery.ContractTests.Identity;

[Collection(ApiCollection.Name)]
public sealed class UsersContractTests(ApiFactory factory)
{
    [Fact]
    public async Task Admin_creates_lists_and_reads_an_account()
    {
        var (_, city) = await TestData.CityAsync(factory);
        var agency = await TestData.AgencyAsync(factory, city.Id);
        var admin = await TestData.UserAsync(factory, RoleName.SUPER_ADMIN);
        var client = await TestData.ClientForAsync(factory, admin);
        var email = TestData.Unique("agent") + "@WIMO.test";

        var created = await client.PostAsJsonAsync("/api/users", new
        {
            email, firstName = "  Sara ", lastName = "Alaoui", phone = "", roles = new[] { "STAFF" }, agencyId = agency.Id, password = "Staff-1234",
        });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var detail = await created.JsonAsync();
        detail.GetProperty("email").GetString().ShouldBe(email.ToLowerInvariant());
        detail.GetProperty("firstName").GetString().ShouldBe("Sara");
        detail.GetProperty("phone").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        detail.GetProperty("agencyName").GetString().ShouldBe(agency.Name);
        detail.GetProperty("agencyIds")[0].GetString().ShouldBe(agency.Id);
        detail.GetProperty("clientId").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        detail.GetProperty("createdAt").GetString()!.ShouldMatch(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z$");

        var list = await (await client.GetAsync($"/api/users?agencyId={agency.Id}&role=STAFF&pageSize=5")).JsonAsync();
        list.GetProperty("total").GetInt32().ShouldBe(1);
        list.GetProperty("page").GetInt32().ShouldBe(1);
        list.GetProperty("pageSize").GetInt32().ShouldBe(5);
        list.GetProperty("items")[0].GetProperty("email").GetString().ShouldBe(email.ToLowerInvariant());

        var duplicate = await client.PostAsJsonAsync("/api/users", new { email, firstName = "Sara", lastName = "Alaoui", roles = new[] { "STAFF" }, password = "Staff-1234" });
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await duplicate.JsonAsync()).GetProperty("details").GetProperty("field").GetString().ShouldBe("email");
    }

    [Fact]
    public async Task Unknown_sort_is_a_422_listing_the_allowed_keys()
    {
        var admin = await TestData.UserAsync(factory, RoleName.SUPER_ADMIN);
        var client = await TestData.ClientForAsync(factory, admin);
        var response = await client.GetAsync("/api/users?sort=nope");
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await response.JsonAsync();
        body.GetProperty("message").GetString().ShouldBe("Tri inconnu : nope");
        body.GetProperty("details").GetProperty("allowed").GetArrayLength().ShouldBe(6);
    }

    [Fact]
    public async Task Bad_page_number_is_coerced_like_zod()
    {
        var admin = await TestData.UserAsync(factory, RoleName.SUPER_ADMIN);
        var client = await TestData.ClientForAsync(factory, admin);
        var response = await client.GetAsync("/api/users?page=abc");
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var issue = (await response.JsonAsync()).GetProperty("details").GetProperty("issues")[0];
        issue.GetProperty("path").GetString().ShouldBe("page");
        issue.GetProperty("message").GetString().ShouldBe("Invalid input: expected number, received NaN");
    }

    [Fact]
    public async Task Staff_without_the_permission_is_refused()
    {
        var merchantUser = await TestData.UserAsync(factory, RoleName.MERCHANT);
        var client = await TestData.ClientForAsync(factory, merchantUser);
        var response = await client.GetAsync("/api/users");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var body = await response.JsonAsync();
        body.GetProperty("message").GetString().ShouldBe("Permission insuffisante pour cette opération.");
        body.GetProperty("details").GetProperty("required")[0].GetString().ShouldBe("accounts.read");
    }

    [Fact]
    public async Task User_override_grants_a_permission_beyond_the_role()
    {
        var admin = await TestData.UserAsync(factory, RoleName.SUPER_ADMIN);
        var adminClient = await TestData.ClientForAsync(factory, admin);
        var staff = await TestData.UserAsync(factory, RoleName.STAFF);

        var current = await (await adminClient.GetAsync($"/api/users/{staff.Id}/permissions")).JsonAsync();
        var effective = current.GetProperty("effective").EnumerateArray().Select(p => p.GetString()!).ToList();
        effective.ShouldNotContain("accounts.write");
        effective.Add("accounts.write");

        var updated = await (await adminClient.PutAsJsonAsync($"/api/users/{staff.Id}/permissions", new { effective })).JsonAsync();
        updated.GetProperty("grants").EnumerateArray().Select(p => p.GetString()).ShouldContain("accounts.write");
        updated.GetProperty("locked").GetBoolean().ShouldBeFalse();

        var staffClient = await TestData.ClientForAsync(factory, staff);
        (await staffClient.GetAsync("/api/users")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var reset = await (await adminClient.DeleteAsync($"/api/users/{staff.Id}/permissions")).JsonAsync();
        reset.GetProperty("grants").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Super_admin_rights_are_locked()
    {
        var admin = await TestData.UserAsync(factory, RoleName.SUPER_ADMIN);
        var other = await TestData.UserAsync(factory, RoleName.SUPER_ADMIN);
        var client = await TestData.ClientForAsync(factory, admin);
        var response = await client.PutAsJsonAsync($"/api/users/{other.Id}/permissions", new { effective = Array.Empty<string>() });
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Role_matrix_lists_tenant_roles_and_permissions()
    {
        var admin = await TestData.UserAsync(factory, RoleName.SUPER_ADMIN);
        var client = await TestData.ClientForAsync(factory, admin);
        var body = await (await client.GetAsync("/api/roles")).JsonAsync();
        body.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ShouldBe(["SUPER_ADMIN", "STAFF", "MERCHANT", "COURIER"]);
        body.GetProperty("permissions").EnumerateArray().ShouldAllBe(p => !p.GetProperty("key").GetString()!.StartsWith("platform."));
        body.GetProperty("granted").GetProperty("SUPER_ADMIN").GetArrayLength().ShouldBe(40);
    }

    [Fact]
    public async Task Team_sweep_an_agent_only_sees_staff_of_his_agency()
    {
        var (_, city) = await TestData.CityAsync(factory);
        var mine = await TestData.AgencyAsync(factory, city.Id);
        var other = await TestData.AgencyAsync(factory, city.Id);
        var agent = await TestData.UserAsync(factory, RoleName.STAFF, mine.Id);
        var colleague = await TestData.UserAsync(factory, RoleName.STAFF, mine.Id);
        var stranger = await TestData.UserAsync(factory, RoleName.STAFF, other.Id);
        var client = await TestData.ClientForAsync(factory, agent);

        var body = await (await client.GetAsync("/api/users/team?pageSize=200")).JsonAsync();
        var ids = body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString()).ToList();
        ids.ShouldContain(colleague.Id);
        ids.ShouldContain(agent.Id);
        ids.ShouldNotContain(stranger.Id);
    }

    [Fact]
    public async Task Deactivating_an_account_closes_its_sessions()
    {
        var admin = await TestData.UserAsync(factory, RoleName.SUPER_ADMIN);
        var adminClient = await TestData.ClientForAsync(factory, admin);
        var staff = await TestData.UserAsync(factory, RoleName.STAFF);
        var staffClient = await TestData.ClientForAsync(factory, staff);
        await Task.Delay(1100);

        var response = await adminClient.PatchAsJsonAsync($"/api/users/{staff.Id}/active", new { isActive = false });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.JsonAsync()).GetProperty("isActive").GetBoolean().ShouldBeFalse();
        (await staffClient.GetAsync("/api/auth/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var self = await adminClient.PatchAsJsonAsync($"/api/users/{admin.Id}/active", new { isActive = false });
        self.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Reset_password_returns_a_temporary_password()
    {
        var admin = await TestData.UserAsync(factory, RoleName.SUPER_ADMIN);
        var adminClient = await TestData.ClientForAsync(factory, admin);
        var staff = await TestData.UserAsync(factory, RoleName.STAFF);

        var response = await adminClient.PostAsync($"/api/users/{staff.Id}/reset-password", null);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.JsonAsync();
        body.GetProperty("temporaryPassword").GetString()!.Length.ShouldBe(14);
        body.GetProperty("user").GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Export_is_an_xlsx_attachment()
    {
        var admin = await TestData.UserAsync(factory, RoleName.SUPER_ADMIN);
        var client = await TestData.ClientForAsync(factory, admin);
        var response = await client.GetAsync("/api/users/export");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        response.Content.Headers.ContentDisposition!.ToString().ShouldStartWith("attachment; filename=\"comptes-");
    }
}
