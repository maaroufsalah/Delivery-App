using System.Net;
using System.Net.Http.Json;
using Delivery.ContractTests.Infrastructure;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.ContractTests.Identity;

[Collection(ApiCollection.Name)]
public sealed class AuthContractTests(ApiFactory factory)
{
    [Fact]
    public async Task Wrong_password_is_a_401_with_the_nest_body()
    {
        var user = await TestData.UserAsync(factory, RoleName.STAFF);
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "nope" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var body = await response.JsonAsync();
        body.GetProperty("code").GetString().ShouldBe("UNAUTHENTICATED");
        body.GetProperty("message").GetString().ShouldBe("Identifiants incorrects.");
    }

    [Fact]
    public async Task Empty_login_body_is_a_zod_422()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await response.JsonAsync();
        body.GetProperty("code").GetString().ShouldBe("VALIDATION_FAILED");
        body.GetProperty("message").GetString().ShouldBe("Saisie invalide.");
        var issue = body.GetProperty("details").GetProperty("issues")[0];
        issue.GetProperty("path").GetString().ShouldBe("password");
        issue.GetProperty("message").GetString().ShouldBe("Invalid input: expected string, received undefined");
    }

    [Fact]
    public async Task Browser_login_sets_http_only_cookies_and_keeps_tokens_out_of_the_body()
    {
        var user = await TestData.UserAsync(factory, RoleName.SUPER_ADMIN);
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { identifier = user.Email!.ToUpperInvariant(), password = TestData.Password, remember = true });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        cookies.ShouldContain(c => c.StartsWith("dp.at=") && c.Contains("httponly") && c.Contains("samesite=lax") && c.Contains("max-age=840"));
        cookies.ShouldContain(c => c.StartsWith("dp.rt=") && c.Contains("max-age=2592000"));
        var body = await response.JsonAsync();
        body.GetProperty("id").GetString().ShouldBe(user.Id);
        body.GetProperty("impersonated").GetBoolean().ShouldBeFalse();
        body.GetProperty("roles")[0].GetString().ShouldBe("SUPER_ADMIN");
        body.GetProperty("agencyId").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        body.TryGetProperty("accessToken", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task A_Laravel_bcrypt_account_logs_in_and_is_rehashed_to_argon2id()
    {
        // `password_hash("Wimo-Laravel-2026!", PASSWORD_BCRYPT)` de PHP 7.4 : l'empreinte reprise de Wimo Laravel telle quelle.
        const string laravel = "$2y$10$YPotekj30Lsbl2jUeUQJhuoENKhmBRW55IFS.9aRUaA.sTTPv2ury";
        var user = await TestData.UserAsync(factory, RoleName.SUPER_ADMIN);
        await factory.ExecuteDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Id == user.Id)).PasswordHash = laravel;
            await db.SaveChangesAsync();
        });
        var client = factory.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/login", new { identifier = user.Email, password = "wrong" })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await factory.QueryDbAsync(db => db.Users.Where(u => u.Id == user.Id).Select(u => u.PasswordHash).SingleAsync())).ShouldBe(laravel);

        (await client.PostAsJsonAsync("/api/auth/login", new { identifier = user.Email, password = "Wimo-Laravel-2026!" })).StatusCode.ShouldBe(HttpStatusCode.OK);
        var stored = await factory.QueryDbAsync(db => db.Users.Where(u => u.Id == user.Id).Select(u => u.PasswordHash).SingleAsync());
        stored.ShouldStartWith("$argon2id$");
        Delivery.Application.Common.Security.PasswordHasher.Verify(stored, "Wimo-Laravel-2026!").ShouldBeTrue();
        (await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { identifier = user.Email, password = "Wimo-Laravel-2026!" })).StatusCode.ShouldBe(HttpStatusCode.OK);
        var journal = await factory.QueryDbAsync(db => db.AuditLogs.Where(a => a.Entity == "User" && a.EntityId == user.Id && a.Action == AuditAction.UPDATE).Select(a => a.After).ToListAsync());
        journal.ShouldAllBe(a => a == null || !a.Contains("argon2id"));
    }

    [Fact]
    public async Task Mobile_login_returns_tokens_and_durations()
    {
        var user = await TestData.UserAsync(factory, RoleName.STAFF);
        var (_, _, body) = await TestData.LoginAsync(factory, user.Email!);
        body.GetProperty("expiresIn").GetInt32().ShouldBe(900);
        body.GetProperty("refreshExpiresIn").GetInt32().ShouldBe(30 * 24 * 3600);
    }

    [Fact]
    public async Task Me_requires_a_session()
    {
        var response = await factory.CreateClient().GetAsync("/api/auth/me");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.JsonAsync()).GetProperty("message").GetString().ShouldBe("Session expirée ou absente.");
    }

    [Fact]
    public async Task Me_returns_the_profile_with_effective_permissions_and_tenant_banner()
    {
        var user = await TestData.UserAsync(factory, RoleName.STAFF);
        var client = await TestData.ClientForAsync(factory, user);
        var body = await (await client.GetAsync("/api/auth/me")).JsonAsync();

        body.GetProperty("email").GetString().ShouldBe(user.Email);
        body.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ShouldContain("parcels.read");
        body.GetProperty("tenant").GetProperty("status").GetString().ShouldBe("ACTIVE");
        body.GetProperty("tenant").GetProperty("expired").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Refresh_rotates_and_a_quick_replay_gets_the_same_child()
    {
        var user = await TestData.UserAsync(factory, RoleName.STAFF);
        var (_, refresh, _) = await TestData.LoginAsync(factory, user.Email!);
        var mobile = factory.CreateMobileClient();

        var first = await (await mobile.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refresh })).JsonAsync();
        var second = await (await mobile.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refresh })).JsonAsync();

        first.GetProperty("refreshToken").GetString().ShouldNotBe(refresh);
        second.GetProperty("refreshToken").GetString().ShouldBe(first.GetProperty("refreshToken").GetString());
    }

    [Fact]
    public async Task Refresh_reuse_after_the_grace_revokes_the_family()
    {
        var user = await TestData.UserAsync(factory, RoleName.STAFF);
        var (_, refresh, _) = await TestData.LoginAsync(factory, user.Email!);
        var mobile = factory.CreateMobileClient();
        (await mobile.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refresh })).StatusCode.ShouldBe(HttpStatusCode.OK);
        await factory.ExecuteDbAsync(db => db.RefreshSessions.Where(s => s.UserId == user.Id && s.RotatedAt != null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RotatedAt, DateTime.UtcNow.AddMinutes(-1))));

        var replay = await mobile.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refresh });

        replay.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await replay.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("refresh-reused");
    }

    [Fact]
    public async Task Refresh_without_token_is_a_401()
    {
        var response = await factory.CreateClient().PostAsync("/api/auth/refresh", null);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.JsonAsync()).GetProperty("message").GetString().ShouldBe("Aucune session à renouveler.");
    }

    [Fact]
    public async Task Logout_revokes_every_token_of_the_account()
    {
        var user = await TestData.UserAsync(factory, RoleName.STAFF);
        var (access, refresh, _) = await TestData.LoginAsync(factory, user.Email!);
        await Task.Delay(1100);
        var mobile = factory.CreateMobileClient();
        ApiFactory.UseBearer(mobile, access);

        var logout = await mobile.PostAsJsonAsync("/api/auth/logout", new { refreshToken = refresh });
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await mobile.GetAsync("/api/auth/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_is_throttled_after_ten_failures()
    {
        var login = TestData.Unique("ghost") + "@wimo.test";
        var client = factory.CreateClient();
        for (var i = 0; i < 10; i++)
        {
            (await client.PostAsJsonAsync("/api/auth/login", new { email = login, password = "x" })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
        var blocked = await client.PostAsJsonAsync("/api/auth/login", new { email = login, password = "x" });

        blocked.StatusCode.ShouldBe((HttpStatusCode)429);
        blocked.Headers.RetryAfter.ShouldNotBeNull();
        (await blocked.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("login-throttled");
    }

    [Fact]
    public async Task Temporary_password_blocks_everything_but_the_password_change()
    {
        var user = await TestData.UserAsync(factory, RoleName.SUPER_ADMIN, mustChangePassword: true);
        var client = await TestData.ClientForAsync(factory, user);

        var blocked = await client.GetAsync("/api/users");
        blocked.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await blocked.JsonAsync()).GetProperty("details").GetProperty("code").GetString().ShouldBe("PASSWORD_CHANGE_REQUIRED");

        var change = await client.PostAsJsonAsync("/api/users/me/password", new { currentPassword = TestData.Password, password = "Another-456!", confirmation = "Another-456!" });
        change.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await change.JsonAsync()).GetProperty("ok").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Public_registration_creates_a_pending_account_that_cannot_log_in()
    {
        var (_, city) = await TestData.CityAsync(factory);
        var username = TestData.Unique("livreur");
        var response = await factory.CreateClient().PostAsJsonAsync("/api/public/register", new
        {
            kind = "COURIER", firstName = "Ali", lastName = "Benali", phone = "06 11 22 33 44", cityId = city.Id, username, password = "Courier-123",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.JsonAsync();
        body.GetProperty("ok").GetBoolean().ShouldBeTrue();
        body.GetProperty("kind").GetString().ShouldBe("COURIER");

        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { identifier = username, password = "Courier-123" });
        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await login.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("pending-validation");
        var phone = await factory.QueryDbAsync(db => db.Couriers.Where(c => c.User!.Username == username).Select(c => c.Phone).FirstAsync());
        phone.ShouldBe("0611223344");
    }

    [Fact]
    public async Task Courier_registration_needs_an_email_or_a_username()
    {
        var (_, city) = await TestData.CityAsync(factory);
        var response = await factory.CreateClient().PostAsJsonAsync("/api/public/register", new
        {
            kind = "COURIER", firstName = "Ali", lastName = "Benali", phone = "0611223344", cityId = city.Id, password = "Courier-123",
        });
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var issue = (await response.JsonAsync()).GetProperty("details").GetProperty("issues")[0];
        issue.GetProperty("path").GetString().ShouldBe("username");
        issue.GetProperty("message").GetString().ShouldBe("email-or-username");
    }

    [Fact]
    public async Task Forgot_password_answers_with_the_company_contact()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/forgot-password", new { email = "nobody@wimo.test" });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.JsonAsync();
        body.GetProperty("ok").GetBoolean().ShouldBeTrue();
        body.GetProperty("companyName").GetString().ShouldBe("Wimo");
        body.GetProperty("emailEnabled").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Health_reports_the_database()
    {
        var body = await (await factory.CreateClient().GetAsync("/api/health")).JsonAsync();
        body.GetProperty("status").GetString().ShouldBe("ok");
        body.GetProperty("database").GetString().ShouldBe("up");
    }

    [Fact]
    public async Task Deleting_my_account_anonymizes_it()
    {
        var user = await TestData.UserAsync(factory, RoleName.STAFF);
        var client = await TestData.ClientForAsync(factory, user);
        var response = await client.DeleteAsync("/api/auth/me");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.JsonAsync()).GetProperty("deleted").GetBoolean().ShouldBeTrue();
        var email = await factory.QueryDbAsync(db => db.Users.Where(u => u.Id == user.Id).Select(u => u.Email).FirstAsync());
        email!.ShouldStartWith("supprime+");
    }
}
