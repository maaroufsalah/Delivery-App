using System.Security.Cryptography;
using System.Text;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Identity.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Identity;
using Delivery.Application.Common.Validation;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Identity.Commands;

/// <summary>
/// `POST /auth/refresh` : le jeton vient du corps pour le mobile, du cookie
/// `dp.rt` sinon (corps en dernier recours). Rotation avec détection de rejeu.
/// </summary>
public sealed record RefreshCommand(string? BodyToken, string? CookieToken) : IRequest<AuthResult>
{
    public string? BodyToken { get; init; } = Zod.Trim(BodyToken);
}

/// <summary>`refreshSchema` : `refreshToken` facultatif, non vide une fois rogné.</summary>
public sealed class RefreshValidator : ZodValidator<RefreshCommand>
{
    public RefreshValidator()
    {
        RuleFor(x => x.BodyToken).MinLength(1).OverridePropertyName("refreshToken");
    }
}

internal sealed class RefreshHandler(IAppDbContext db, IJwtService jwt, IClock clock, ICurrentUser caller)
    : IRequestHandler<RefreshCommand, AuthResult>
{
    private static readonly TimeSpan ChildWait = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ChildPoll = TimeSpan.FromMilliseconds(100);

    public async Task<AuthResult> Handle(RefreshCommand request, CancellationToken cancellationToken)
    {
        var fromBody = caller.IsMobileClient ? request.BodyToken : null;
        var token = fromBody ?? request.CookieToken ?? request.BodyToken;
        if (string.IsNullOrEmpty(token)) throw BusinessException.Unauthenticated("Aucune session à renouveler.");
        var session = await RefreshAsync(token, cancellationToken);
        return new AuthResult(session, fromBody is not null);
    }

    private async Task<IssuedSession> RefreshAsync(string token, CancellationToken cancellationToken)
    {
        var payload = jwt.Verify(JwtKind.Refresh, token) ?? throw BusinessException.Unauthenticated("Session expirée.");
        var user = await Sessions.LoadUserAsync(db, payload.Sub, cancellationToken);
        if (user is null || !user.IsActive) throw BusinessException.Unauthenticated("Compte inactif.");
        var issuedAt = DateTime.UnixEpoch.AddSeconds(payload.Iat ?? 0);
        if (issuedAt < user.TokensValidFrom) throw BusinessException.Unauthenticated("Session révoquée.");

        var companyId = await Sessions.CompanyIdAsync(db, cancellationToken);
        var identity = Sessions.IdentityOf(user, companyId);
        var kind = SessionPolicy.KindOf(payload.Skd);
        var id = payload.Jti ?? "legacy:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var now = clock.UtcNow;

        if (payload.Jti is null)
        {
            var exists = await db.RefreshSessions.AnyAsync(s => s.Id == id, cancellationToken);
            if (!exists)
            {
                db.RefreshSessions.Add(new RefreshSession
                {
                    Id = id,
                    UserId = user.Id,
                    FamilyId = Guid.NewGuid().ToString(),
                    Kind = kind,
                    IssuedAt = issuedAt,
                    AccessExpiresAt = issuedAt,
                    ExpiresAt = DateTime.UnixEpoch.AddSeconds(payload.Exp ?? 0),
                    CreatedAt = now,
                });
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        var claimed = await db.RefreshSessions
            .Where(s => s.Id == id && s.UserId == user.Id && s.RotatedAt == null && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RotatedAt, now), cancellationToken);
        var row = await db.RefreshSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (row is null || row.UserId != user.Id || row.RevokedAt is not null) throw BusinessException.Unauthenticated("Session révoquée.");

        if (claimed == 1)
        {
            return await Sessions.IssueAsync(db, jwt, clock, identity, kind, false, row, cancellationToken);
        }

        var rotatedAt = row.RotatedAt ?? DateTime.UnixEpoch;
        if (now - rotatedAt <= SessionPolicy.ReuseGrace)
        {
            var child = await ChildOfAsync(row.Id, cancellationToken);
            if (child is { RevokedAt: null, RotatedAt: null })
            {
                return Sessions.Sign(jwt, identity, kind, false, child, RefreshSession.ToSeconds(clock.UtcNow));
            }
        }

        await Sessions.RevokeAllAsync(db, clock, user.Id, cancellationToken);
        throw BusinessException.Unauthenticated("Session révoquée.", new { reason = "refresh-reused" });
    }

    /// <summary>Le parent vient d'être tourné par une requête concurrente : on attend son enfant (2 s).</summary>
    private async Task<RefreshSession?> ChildOfAsync(string parentId, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + ChildWait;
        while (true)
        {
            var childId = await db.RefreshSessions.AsNoTracking().Where(s => s.Id == parentId).Select(s => s.ReplacedById).FirstOrDefaultAsync(cancellationToken);
            if (childId is not null)
            {
                return await db.RefreshSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == childId, cancellationToken);
            }
            if (DateTime.UtcNow >= deadline) return null;
            await Task.Delay(ChildPoll, cancellationToken);
        }
    }
}
