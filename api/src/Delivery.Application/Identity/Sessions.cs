using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Settings;
using Delivery.Application.Identity.Dtos;
using Delivery.Domain.Entities;
using Delivery.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Identity;

/// <summary>
/// Émission et révocation des sessions (`issueSession`, `signSession`,
/// `revokeAll` de l'`AuthService`), partagées par connexion, renouvellement
/// et accès console.
/// </summary>
internal static class Sessions
{
    public static async Task<IssuedSession> IssueAsync(
        IAppDbContext db, IJwtService jwt, IClock clock, SessionIdentity identity, string kind, bool impersonated,
        RefreshSession? parent, CancellationToken cancellationToken)
    {
        var durations = await db.SessionDurationsAsync(cancellationToken);
        var now = clock.UtcNow;
        var iat = RefreshSession.ToSeconds(now);
        var row = RefreshSession.Open(identity.UserId, parent?.FamilyId ?? Guid.NewGuid().ToString(), kind, iat, durations, now);
        db.RefreshSessions.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        if (parent is not null)
        {
            await db.RefreshSessions.Where(s => s.Id == parent.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReplacedById, row.Id), cancellationToken);
        }
        await db.RefreshSessions
            .Where(s => s.UserId == identity.UserId && s.ExpiresAt < row.IssuedAt)
            .ExecuteDeleteAsync(cancellationToken);
        return Sign(jwt, identity, kind, impersonated, row, iat);
    }

    public static IssuedSession Sign(IJwtService jwt, SessionIdentity identity, string kind, bool impersonated, RefreshSession row, long nowSeconds)
    {
        var iat = RefreshSession.ToSeconds(row.IssuedAt);
        var accessExp = RefreshSession.ToSeconds(row.AccessExpiresAt);
        var refreshExp = RefreshSession.ToSeconds(row.ExpiresAt);
        var payload = new Dictionary<string, object?>
        {
            ["sub"] = identity.UserId,
            ["cid"] = identity.CompanyId,
            ["email"] = identity.Email,
            ["roles"] = identity.Roles,
            ["aid"] = identity.AgencyId,
            ["clid"] = identity.ClientId,
            ["crid"] = identity.CourierId,
            ["mcp"] = identity.MustChangePassword,
            ["skd"] = kind,
        };
        if (impersonated) payload["imp"] = true;
        var access = new Dictionary<string, object?>(payload) { ["iat"] = iat, ["exp"] = accessExp };
        var refresh = new Dictionary<string, object?>(payload) { ["jti"] = row.Id, ["iat"] = iat, ["exp"] = refreshExp };
        return new IssuedSession(
            identity,
            jwt.Sign(JwtKind.Access, access),
            jwt.Sign(JwtKind.Refresh, refresh),
            (int)Math.Max(accessExp - nowSeconds, 0),
            (int)Math.Max(refreshExp - nowSeconds, 0),
            kind,
            impersonated);
    }

    /// <summary>`revokeAll` : plus aucun jeton émis avant maintenant, sessions de rafraîchissement closes.</summary>
    public static async Task RevokeAllAsync(IAppDbContext db, IClock clock, string userId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(u => u.SetProperty(x => x.TokensValidFrom, now), cancellationToken);
        await CloseAsync(db, userId, now, cancellationToken);
    }

    /// <summary>`SessionStateService.close` : révoque les sessions de rafraîchissement ouvertes.</summary>
    public static Task CloseAsync(IAppDbContext db, string userId, DateTime now, CancellationToken cancellationToken) =>
        db.RefreshSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), cancellationToken);

    public static async Task<User?> LoadUserAsync(IAppDbContext db, string userId, CancellationToken cancellationToken) =>
        await db.Users
            .Include(u => u.Roles)
            .Include(u => u.Client)
            .Include(u => u.Courier)
            .FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null, cancellationToken);

    public static async Task<string?> CompanyIdAsync(IAppDbContext db, CancellationToken cancellationToken) =>
        await db.Companies.AsNoTracking().OrderBy(c => c.CreatedAt).Select(c => c.Id).FirstOrDefaultAsync(cancellationToken);

    public static SessionIdentity IdentityOf(User user, string? companyId, bool? mustChangePassword = null) => new(
        user.Id,
        companyId,
        user.LoginLabel,
        user.Roles.Select(r => r.Role.ToString()).ToList(),
        user.AgencyId,
        user.Client?.Id,
        user.Courier?.Id,
        mustChangePassword ?? user.MustChangePassword);
}
