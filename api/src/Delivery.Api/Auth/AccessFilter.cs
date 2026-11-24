using System.Collections.Concurrent;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Api.Auth;

/// <summary>
/// Les gardes de Speedy, dans leur ordre : `JwtAuthGuard` (session), puis
/// `PermissionsGuard` (mot de passe provisoire, permissions), puis les
/// `@UseGuards` du contrôleur et de la méthode. Avant toute liaison du corps,
/// donc avant toute validation, comme dans Nest.
/// </summary>
public sealed class AccessFilter(IJwtService jwt, IAppDbContext db, PublicRateLimiter rateLimiter, IClock clock) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor action) return;
        var http = context.HttpContext;
        var isPublic = Has<PublicAttribute>(action);
        AuthenticatedUser? user = null;

        if (!isPublic)
        {
            user = await AuthenticateAsync(http) ?? throw BusinessException.Unauthenticated("Session expirée ou absente.");
            http.Items[CurrentUser.ItemKey] = user;
            await AuthorizeAsync(action, user, http.RequestAborted);
        }

        foreach (var guard in GuardsOf(action))
        {
            if (guard == Guard.ProviderToken) user = await ProviderAsync(http);
            else Check(guard, user, http, action);
        }
    }

    /// <summary>
    /// `ProviderTokenGuard` : jeton actif (ou l'ancien pendant sa grâce), livreur et compte actifs, débit par minute,
    /// permission `parcels.status`. Le débit se compte en base sur le journal des appels (plusieurs instances).
    /// </summary>
    private async Task<AuthenticatedUser> ProviderAsync(HttpContext http)
    {
        var token = ProviderBearer(http.Request.Headers.Authorization.ToString())
                    ?? throw BusinessException.Unauthenticated("Jeton de prestataire absent ou mal formé.", new { reason = "provider-token-missing" });
        var hash = Application.Providers.ProviderTokens.Hash(token);
        var now = clock.UtcNow;
        var row = await db.ProviderTokens.AsNoTracking()
                      .Where(t => t.RevokedAt == null && (t.TokenHash == hash || (t.PreviousTokenHash == hash && t.PreviousExpiresAt > now)))
                      .Select(t => new
                      {
                          t.Id, t.Name, t.Mode, t.StatusMap, t.RateLimitPerMinute, t.LastUsedAt, t.UserId, t.CourierId, CourierActive = t.Courier.IsActive, CourierDeleted = t.Courier.DeletedAt,
                          CourierUserId = t.Courier.UserId, UserActive = t.User.IsActive, UserDeleted = t.User.DeletedAt, t.User.Email, t.User.Username, t.User.AgencyId,
                          Roles = t.User.Roles.Select(r => r.Role).ToList(),
                      }).FirstOrDefaultAsync(http.RequestAborted)
                  ?? throw BusinessException.Unauthenticated("Jeton de prestataire invalide.", new { reason = "provider-token-invalid" });
        if (!row.CourierActive || row.CourierDeleted is not null || row.CourierUserId != row.UserId || !row.UserActive || row.UserDeleted is not null)
        {
            throw BusinessException.Forbidden("Le livreur de ce prestataire est désactivé.", new { reason = "provider-courier-inactive" });
        }
        var windowStart = now.AddMinutes(-1);
        var recent = await db.ProviderCalls.AsNoTracking().Where(c => c.TokenId == row.Id && c.At > windowStart).Select(c => c.At).OrderBy(at => at).ToListAsync(http.RequestAborted);
        if (recent.Count >= row.RateLimitPerMinute)
        {
            var retryAfter = Math.Max(1, (int)Math.Ceiling((recent[recent.Count - row.RateLimitPerMinute] - windowStart).TotalSeconds));
            db.ProviderCalls.Add(new Domain.Entities.ProviderCall
            {
                TokenId = row.Id, EventKey = Header(http, "idempotency-key"), ParcelCode = (http.Request.RouteValues["code"] as string ?? string.Empty) is var c && c.Length > 80 ? c[..80] : c,
                ProviderStatus = 0, Mode = row.Mode, Outcome = ProviderCallOutcome.REJECTED, HttpStatus = 429, Reason = "rate-limited", RequestId = http.TraceIdentifier, At = now,
            });
            await db.SaveChangesAsync(http.RequestAborted);
            throw BusinessException.TooManyRequests("Trop de requêtes. Réessayez dans une minute.", new { reason = "rate-limited", retryAfterSeconds = retryAfter });
        }
        var user = new AuthenticatedUser(row.UserId, null, row.Email ?? row.Username ?? row.Name, row.Roles, row.AgencyId, null, row.CourierId, false, false, null);
        if (!user.Roles.Contains(RoleName.SUPER_ADMIN) && !(await db.EffectivePermissionsAsync(user.UserId, user.Roles, http.RequestAborted)).Contains("parcels.status"))
        {
            throw BusinessException.Forbidden("Le livreur de ce prestataire ne peut pas changer un statut.", new { reason = "provider-permission-missing", required = new[] { "parcels.status" } });
        }
        http.Items[CurrentUser.ItemKey] = user;
        http.Items[ProviderItemKey] = new Application.Providers.ProviderContext(row.Id, row.Name, row.Mode, row.StatusMap);
        if (row.LastUsedAt is null || (now - row.LastUsedAt.Value).TotalSeconds >= 60)
        {
            await db.ProviderTokens.Where(t => t.Id == row.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.LastUsedAt, now), http.RequestAborted);
        }
        return user;
    }

    public const string ProviderItemKey = "provider-token";

    private static string? ProviderBearer(string? header)
    {
        var match = System.Text.RegularExpressions.Regex.Match(header?.Trim() ?? string.Empty, @"^Bearer\s+(\S+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var token = match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
        return token is not null && System.Text.RegularExpressions.Regex.IsMatch(token, "^[a-f0-9]{64}$") ? token : null;
    }

    private static string? Header(HttpContext http, string name) => http.Request.Headers.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value.ToString()) ? value.ToString().Trim() : null;

    private async Task<AuthenticatedUser?> AuthenticateAsync(HttpContext http)
    {
        var token = AuthCookies.Read(http.Request, AuthCookies.Access) ?? AuthCookies.Bearer(http.Request);
        if (token is null) return null;
        var payload = jwt.Verify(JwtKind.Access, token);
        if (payload is null || payload.Purpose is not null) return null;

        // `SessionStateService.assertOpen` : compte ouvert, jeton postérieur à la dernière révocation.
        var state = await db.Users.AsNoTracking()
            .Where(u => u.Id == payload.Sub)
            .Select(u => new { u.IsActive, u.DeletedAt, u.TokensValidFrom })
            .FirstOrDefaultAsync(http.RequestAborted);
        if (state is null || !state.IsActive || state.DeletedAt is not null) return null;
        var validFrom = (long)Math.Floor((state.TokensValidFrom - DateTime.UnixEpoch).TotalSeconds);
        if (payload.Iat is null || payload.Iat < validFrom) return null;

        return new AuthenticatedUser(
            payload.Sub,
            payload.Cid,
            payload.Email,
            payload.Roles.Select(r => Enum.TryParse<RoleName>(r, out var role) ? role : (RoleName?)null).OfType<RoleName>().ToList(),
            payload.Aid,
            payload.Clid,
            payload.Crid,
            payload.Mcp,
            payload.Imp,
            payload.Iat);
    }

    private async Task AuthorizeAsync(ControllerActionDescriptor action, AuthenticatedUser user, CancellationToken cancellationToken)
    {
        if (user.MustChangePassword)
        {
            if (!Has<AllowPasswordChangeAttribute>(action))
            {
                throw BusinessException.Forbidden("Mot de passe temporaire : changez-le avant toute autre opération.", new { code = "PASSWORD_CHANGE_REQUIRED" });
            }
            return;
        }

        var required = action.MethodInfo.GetCustomAttributes(typeof(RequirePermissionAttribute), true).Cast<RequirePermissionAttribute>()
            .Concat(action.ControllerTypeInfo.GetCustomAttributes(typeof(RequirePermissionAttribute), true).Cast<RequirePermissionAttribute>())
            .FirstOrDefault()?.Permissions;
        if (required is null || required.Count == 0) return;

        var isPlatformRoute = required.Any(p => p.StartsWith("platform.", StringComparison.Ordinal));
        if (isPlatformRoute && !user.Roles.Contains(RoleName.PLATFORM_ADMIN))
        {
            throw BusinessException.Forbidden("Réservé à un administrateur de plateforme.", new { required });
        }
        if (!isPlatformRoute && user.Roles.Contains(RoleName.SUPER_ADMIN)) return;

        var granted = await db.EffectivePermissionsAsync(user.UserId, user.Roles, cancellationToken);
        if (!required.Any(granted.Contains))
        {
            throw BusinessException.Forbidden("Permission insuffisante pour cette opération.", new { required });
        }
    }

    private void Check(Guard guard, AuthenticatedUser? user, HttpContext http, ControllerActionDescriptor action)
    {
        switch (guard)
        {
            case Guard.NotMerchant when user?.ClientId is not null:
                throw BusinessException.Forbidden("Ce geste appartient au transporteur : il ne vous est pas ouvert.", new { reason = "merchant-cannot-do-this" });
            case Guard.Merchant when user?.ClientId is null:
                throw BusinessException.Forbidden("Cet espace est réservé aux e-commerçants.", new { reason = "not-a-merchant" });
            case Guard.Courier when user?.CourierId is null:
                throw BusinessException.Forbidden("Cet espace est réservé aux livreurs.", new { reason = "not-a-courier" });
            case Guard.Staff when user is null || user.ClientId is not null || user.CourierId is not null:
                throw BusinessException.Forbidden("Cet espace est réservé au personnel.", new { reason = "not-a-staff" });
            case Guard.TechnicalView when !(user?.Impersonated == true || user?.Roles.Contains(RoleName.PLATFORM_ADMIN) == true):
                throw BusinessException.Forbidden("Vue technique réservée à l’équipe Nocido.", new { reason = "technical-view-only" });
            case Guard.PublicRateLimit:
                rateLimiter.Hit(action.ControllerTypeInfo.FullName ?? action.ControllerName, http.Connection.RemoteIpAddress?.ToString() ?? "unknown", clock.UtcNow);
                break;
        }
    }

    private static IEnumerable<Guard> GuardsOf(ControllerActionDescriptor action) =>
        action.ControllerTypeInfo.GetCustomAttributes(typeof(GuardsAttribute), true).Cast<GuardsAttribute>().SelectMany(g => g.Guards)
            .Concat(action.MethodInfo.GetCustomAttributes(typeof(GuardsAttribute), true).Cast<GuardsAttribute>().SelectMany(g => g.Guards));

    private static bool Has<T>(ControllerActionDescriptor action) where T : Attribute =>
        action.MethodInfo.IsDefined(typeof(T), true) || action.ControllerTypeInfo.IsDefined(typeof(T), true);
}

/// <summary>`PublicRateLimitGuard` : 60 requêtes par minute et par adresse, un compteur par contrôleur.</summary>
public sealed class PublicRateLimiter
{
    private const int MaxPerWindow = 60;
    private const int MaxKeys = 10_000;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private readonly ConcurrentDictionary<string, (int Count, DateTime ResetAt)> _windows = new(StringComparer.Ordinal);

    public void Hit(string scope, string address, DateTime now)
    {
        var key = $"{scope}|{address}";
        lock (_windows)
        {
            if (!_windows.TryGetValue(key, out var window) || now >= window.ResetAt)
            {
                if (_windows.Count >= MaxKeys)
                {
                    foreach (var (k, w) in _windows) if (now >= w.ResetAt) _windows.TryRemove(k, out _);
                    if (_windows.Count >= MaxKeys) _windows.Clear();
                }
                _windows[key] = (1, now + Window);
                return;
            }
            window.Count += 1;
            _windows[key] = window;
            if (window.Count > MaxPerWindow)
            {
                throw BusinessException.TooManyRequests("Trop de requêtes. Réessayez dans une minute.", new
                {
                    reason = "rate-limited",
                    retryAfterSeconds = (int)Math.Ceiling((window.ResetAt - now).TotalMilliseconds / 1000),
                });
            }
        }
    }
}
