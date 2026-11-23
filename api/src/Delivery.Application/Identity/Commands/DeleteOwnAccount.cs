using Delivery.Application.Common.Interfaces;
using Delivery.Application.Identity.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Identity.Commands;

/// <summary>`DELETE /auth/me` : anonymisation du compte (exigence des stores), historique conservé.</summary>
public sealed record DeleteOwnAccountCommand : IRequest<DeletedResult>;

internal sealed class DeleteOwnAccountHandler(IAppDbContext db, ICurrentUser caller, IClock clock, ILogger<DeleteOwnAccountHandler> logger)
    : IRequestHandler<DeleteOwnAccountCommand, DeletedResult>
{
    public async Task<DeletedResult> Handle(DeleteOwnAccountCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .Include(u => u.Roles)
            .Include(u => u.Courier)
            .FirstOrDefaultAsync(u => u.Id == caller.UserId && u.DeletedAt == null, cancellationToken)
            ?? throw BusinessException.NotFound("Compte introuvable.");
        if (user.HasRole(RoleName.SUPER_ADMIN))
        {
            throw BusinessException.Conflict(
                "Un compte propriétaire ne se supprime pas depuis l’application : transférez le rôle à un autre compte, ou contactez votre administrateur.",
                new { reason = "owner-account" });
        }

        var now = clock.UtcNow;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var email = user.Anonymize(now);
            if (user.Courier is { } courier)
            {
                courier.FirstName = "Compte";
                courier.LastName = "supprimé";
                courier.Phone = string.Empty;
                courier.NationalId = null;
                courier.BankName = null;
                courier.Rib = null;
                courier.IsActive = false;
                courier.DeletedAt = now;
            }
            await db.SaveChangesAsync(cancellationToken);
            await db.AuditLogs.Where(a => a.UserId == user.Id)
                .ExecuteUpdateAsync(a => a.SetProperty(x => x.UserEmail, email).SetProperty(x => x.Ip, (string?)null).SetProperty(x => x.UserAgent, (string?)null), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        logger.LogInformation("Compte anonymisé : {UserId}", user.Id);

        try
        {
            await Sessions.RevokeAllAsync(db, clock, user.Id, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // `.catch(() => undefined)`.
        }
        return new DeletedResult(true);
    }
}
