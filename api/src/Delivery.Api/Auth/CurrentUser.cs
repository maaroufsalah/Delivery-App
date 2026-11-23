using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Enums;

namespace Delivery.Api.Auth;

/// <summary>Identité vérifiée d'une requête (sortie de `JwtStrategy.validate`).</summary>
public sealed record AuthenticatedUser(
    string UserId,
    string? CompanyId,
    string Email,
    IReadOnlyList<RoleName> Roles,
    string? AgencyId,
    string? ClientId,
    string? CourierId,
    bool MustChangePassword,
    bool Impersonated,
    long? IssuedAt);

/// <summary><see cref="ICurrentUser"/> lu sur le contexte HTTP courant.</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public const string ItemKey = "Delivery.AuthenticatedUser";

    private HttpContext? Context => accessor.HttpContext;

    private AuthenticatedUser? User => Context?.Items.TryGetValue(ItemKey, out var value) == true ? value as AuthenticatedUser : null;

    public bool IsAuthenticated => User is not null;

    public string UserId => User?.UserId ?? string.Empty;

    public string? CompanyId => User?.CompanyId;

    public string Email => User?.Email ?? string.Empty;

    public IReadOnlyList<RoleName> Roles => User?.Roles ?? [];

    public string? AgencyId => User?.AgencyId;

    public string? ClientId => User?.ClientId;

    public string? CourierId => User?.CourierId;

    public bool MustChangePassword => User?.MustChangePassword ?? false;

    public bool Impersonated => User?.Impersonated ?? false;

    public long? IssuedAt => User?.IssuedAt;

    public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => Context?.Request.Headers.UserAgent.FirstOrDefault();

    public bool IsMobileClient => string.Equals(Context?.Request.Headers["x-client"].FirstOrDefault()?.Trim(), "mobile", StringComparison.OrdinalIgnoreCase);

    public string RequestId => Context?.TraceIdentifier ?? Guid.NewGuid().ToString();

    public string? PreferredLocale
    {
        get
        {
            var request = Context?.Request;
            if (request is null) return null;
            if (request.Cookies.TryGetValue("NEXT_LOCALE", out var chosen) && chosen.Length > 0) return chosen;
            var header = request.Headers.AcceptLanguage.FirstOrDefault();
            return header?.Split(',')[0].Split('-', ';')[0].Trim().ToLowerInvariant();
        }
    }
}
