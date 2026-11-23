using Delivery.Application.Common.Interfaces;
using Delivery.Application.Identity;
using Delivery.Application.Users.Dtos;
using Delivery.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Users.Commands;

/// <summary>`DELETE /users/:id` : suppression douce, sessions fermées.</summary>
public sealed record RemoveUserCommand(string Id) : IRequest<IdResult>;

internal sealed class RemoveUserHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<RemoveUserCommand, IdResult>
{
    public async Task<IdResult> Handle(RemoveUserCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.Id && u.DeletedAt == null, cancellationToken)
            ?? throw BusinessException.NotFound("Compte introuvable.");
        if (request.Id == caller.UserId) throw BusinessException.Validation("Un compte ne peut pas se supprimer lui-même.");
        await UserFields.AssertNotLastSuperAdminAsync(db, request.Id, cancellationToken);
        var now = clock.UtcNow;
        user.SoftDelete(now);
        await db.SaveChangesAsync(cancellationToken);
        await Sessions.CloseAsync(db, user.Id, now, cancellationToken);
        return new IdResult(user.Id);
    }
}
