using System.Text.Json.Serialization;

namespace Delivery.Application.Identity.Dtos;

/// <summary>Identité portée par les jetons (`AuthenticatedUser`).</summary>
public sealed record SessionIdentity(
    string UserId,
    string? CompanyId,
    string Email,
    IReadOnlyList<string> Roles,
    string? AgencyId,
    string? ClientId,
    string? CourierId,
    bool MustChangePassword);

/// <summary>Couple de jetons émis, et durées pour les cookies.</summary>
public sealed record IssuedSession(
    SessionIdentity User,
    string AccessToken,
    string RefreshToken,
    int AccessSeconds,
    int RefreshSeconds,
    string Kind,
    bool Impersonated);

/// <summary>Résultat d'une connexion : la session et le choix de rendre les jetons dans le corps.</summary>
public sealed record AuthResult(IssuedSession Session, bool TokensInBody)
{
    public AuthResponse ToResponse()
    {
        var user = Session.User;
        var profile = new AuthResponse(user.UserId, Session.Impersonated, user.Email, user.Roles, user.AgencyId, user.ClientId, user.CourierId, user.MustChangePassword);
        return TokensInBody
            ? profile with { AccessToken = Session.AccessToken, RefreshToken = Session.RefreshToken, ExpiresIn = Session.AccessSeconds, RefreshExpiresIn = Session.RefreshSeconds }
            : profile;
    }
}

/// <summary>`toProfile` + jetons du client mobile.</summary>
public sealed record AuthResponse(
    string Id,
    bool Impersonated,
    string Email,
    IReadOnlyList<string> Roles,
    string? AgencyId,
    string? ClientId,
    string? CourierId,
    bool MustChangePassword)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AccessToken { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RefreshToken { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ExpiresIn { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RefreshExpiresIn { get; init; }
}

public sealed record TenantBanner(string Status, string? TrialEndsAt, int? TrialDaysLeft, bool Expired);

/// <summary>`UserProfile` de `GET /auth/me`.</summary>
public sealed record UserProfileDto(
    string Id,
    string? Email,
    string? Username,
    string FirstName,
    string LastName,
    string? Phone,
    IReadOnlyList<string> Roles,
    string? AgencyId,
    string? ClientId,
    string? CourierId,
    bool MustChangePassword,
    IReadOnlyList<string> Permissions,
    TenantBanner? Tenant);

public sealed record ForgotPasswordResult(bool Ok, string? SupportPhone, string CompanyName, bool EmailEnabled);

public sealed record DeletedResult(bool Deleted);

public sealed record RegistrationResult(bool Ok, string Kind);

public sealed record HealthDto(string Status, string Database, long Uptime);
