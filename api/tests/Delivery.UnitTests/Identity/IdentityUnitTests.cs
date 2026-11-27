using Delivery.Application.Common.Security;
using Delivery.Application.Identity;
using Delivery.Application.Identity.Commands;
using Delivery.Application.Users.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Identity;
using FluentValidation;

namespace Delivery.UnitTests.Identity;

public sealed class PasswordHasherTests
{
    [Fact]
    public void Verifies_a_hash_produced_by_node_rs_argon2()
    {
        const string speedy = "$argon2id$v=19$m=19456,t=2,p=1$PN9G8dqJLHd8Sng92kEcXg$g7+mSo0p9ihGICuRqULfZk88a9qvz5RBbVPDr0Ipooc";
        PasswordHasher.Verify(speedy, "Speedy-2026!").ShouldBeTrue();
        PasswordHasher.Verify(speedy, "speedy-2026!").ShouldBeFalse();
        PasswordHasher.NeedsRehash(speedy).ShouldBeFalse();
    }

    /// <summary>Empreinte produite par PHP 7.4 `password_hash("Wimo-Laravel-2026!", PASSWORD_BCRYPT)`, comme celles de Wimo Laravel.</summary>
    public const string LaravelHash = "$2y$10$6.zqBSjhlL2CzvM7Vahv2ept8qxW7HjTIVFGaF9mUJMNEf2s94SWu";

    [Fact]
    public void Verifies_a_bcrypt_hash_produced_by_php_password_hash_and_asks_for_a_rehash()
    {
        PasswordHasher.Verify(LaravelHash, "Wimo-Laravel-2026!").ShouldBeTrue();
        PasswordHasher.Verify(LaravelHash, "wimo-laravel-2026!").ShouldBeFalse();
        PasswordHasher.Verify("$2y$10$tronque", "Wimo-Laravel-2026!").ShouldBeFalse();
        PasswordHasher.NeedsRehash(LaravelHash).ShouldBeTrue();
        var rehashed = PasswordHasher.Hash("Wimo-Laravel-2026!");
        PasswordHasher.NeedsRehash(rehashed).ShouldBeFalse();
        PasswordHasher.Verify(rehashed, "Wimo-Laravel-2026!").ShouldBeTrue();
    }

    [Fact]
    public void Round_trips_and_rejects_garbage()
    {
        var hash = PasswordHasher.Hash("Secret-123!");
        hash.ShouldStartWith("$argon2id$v=19$m=19456,t=2,p=1$");
        PasswordHasher.Verify(hash, "Secret-123!").ShouldBeTrue();
        PasswordHasher.Verify("deleted:abc", "x").ShouldBeFalse();
    }
}

public sealed class SessionPolicyTests
{
    [Fact]
    public void Defaults_are_15_minutes_7_days_30_days() =>
        SessionPolicy.From(null, null, null, null).ShouldBe(new SessionDurations(900, 7 * 86400, 30 * 86400, 30 * 86400));

    [Fact]
    public void Settings_are_bounded() =>
        SessionPolicy.From(1, 200, 0.5, 10).ShouldBe(new SessionDurations(300, 90 * 86400, 86400, 10 * 86400));

    [Theory]
    [InlineData(900, 840)]
    [InlineData(60, 30)]
    public void Access_cookie_dies_a_minute_before_its_token(int seconds, int expected) =>
        SessionPolicy.AccessCookieSeconds(seconds).ShouldBe(expected);

    [Fact]
    public void Unknown_kind_is_web() => SessionPolicy.KindOf("tablet").ShouldBe(SessionPolicy.Web);
}

public sealed class LoginThrottleTests
{
    [Fact]
    public void Blocks_the_eleventh_attempt_for_the_same_login_then_releases()
    {
        var throttle = new LoginThrottle();
        var now = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 10; i++) throttle.RecordFailure("1.2.3.4", " Ali@Wimo.ma ", now);

        var error = Should.Throw<BusinessException>(() => throttle.AssertAllowed("1.2.3.4", "ali@wimo.ma", now.AddMinutes(1)));
        error.Code.ShouldBe(ErrorCode.TOO_MANY_REQUESTS);
        Should.NotThrow(() => throttle.AssertAllowed("1.2.3.4", "other@wimo.ma", now.AddMinutes(1)));
        Should.NotThrow(() => throttle.AssertAllowed("1.2.3.4", "ali@wimo.ma", now.AddMinutes(16)));
    }
}

public sealed class UserBehaviorTests
{
    [Fact]
    public void Anonymize_wipes_identity_and_revokes_tokens()
    {
        var now = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var user = new User { Email = "a@b.ma", Username = "ali", FirstName = "Ali", LastName = "B", Phone = "06" };
        var email = user.Anonymize(now);
        email.ShouldStartWith("supprime+");
        user.Email.ShouldBe(email);
        user.Username.ShouldBeNull();
        user.FirstName.ShouldBe("Compte");
        user.PasswordHash.ShouldStartWith("deleted:");
        user.TokensValidFrom.ShouldBe(now);
        user.IsActive.ShouldBeFalse();
    }
}

public sealed class IdentityValidatorTests
{
    private static List<(string Path, string Message)> Issues<T>(IValidator<T> validator, T value) =>
        validator.Validate(value).Errors.Select(e => (e.PropertyName.Length == 0 ? string.Empty : char.ToLowerInvariant(e.PropertyName[0]) + e.PropertyName[1..], e.ErrorMessage)).ToList();

    [Fact]
    public void Login_needs_an_identifier()
    {
        Issues(new LoginValidator(), new LoginCommand { Password = "x" }).ShouldBe([("identifier", "required")]);
    }

    [Fact]
    public void Login_identifier_is_trimmed_before_checks()
    {
        Issues(new LoginValidator(), new LoginCommand { Identifier = "   ", Password = "x" }).ShouldBe([("identifier", "required")]);
    }

    [Fact]
    public void Create_user_requires_an_email_unless_courier_with_username()
    {
        var staff = new CreateUserCommand { FirstName = "Ali", LastName = "Bena", Roles = ["STAFF"], Password = "12345678" };
        Issues(new CreateUserValidator(), staff).ShouldBe([("email", "required")]);

        var courier = staff with { Roles = ["COURIER"], Username = "Ali.B" };
        Issues(new CreateUserValidator(), courier).ShouldBeEmpty();
        courier.Username.ShouldBe("ali.b");
    }

    [Fact]
    public void Password_change_refinements_run_after_field_rules()
    {
        Issues(new ChangeOwnPasswordValidator(), new ChangeOwnPasswordCommand { CurrentPassword = "a", Password = "short", Confirmation = "x" })
            .ShouldBe([("password", "password-too-short")]);
        Issues(new ChangeOwnPasswordValidator(), new ChangeOwnPasswordCommand { CurrentPassword = "same-pass", Password = "same-pass", Confirmation = "other-pass" })
            .ShouldBe([("confirmation", "password-mismatch"), ("password", "password-unchanged")]);
    }

    [Fact]
    public void Registration_normalizes_the_phone()
    {
        var command = new RegisterCommand { Phone = "+212 (6) 11-22.33 44" };
        command.Phone.ShouldBe("+212611223344");
        new RegisterCommand { Phone = "00212611" }.Phone.ShouldBe("+212611");
    }
}
