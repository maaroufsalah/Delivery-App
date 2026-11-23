using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Identity;
using Delivery.Application.Users.Dtos;
using Delivery.Application.Users.Queries;
using Delivery.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Users.Commands;

/// <summary>`POST /users/:id/reset-password` : mot de passe provisoire de 14 caractères, à changer.</summary>
public sealed record ResetUserPasswordCommand(string Id) : IRequest<UserInvitationDto>;

internal sealed class ResetUserPasswordHandler(IAppDbContext db, IClock clock, IMapper mapper) : IRequestHandler<ResetUserPasswordCommand, UserInvitationDto>
{
    public async Task<UserInvitationDto> Handle(ResetUserPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.Id && u.DeletedAt == null, cancellationToken)
            ?? throw BusinessException.NotFound("Compte introuvable.");
        var temporaryPassword = UserFields.GeneratePassword();
        var now = clock.UtcNow;
        user.ChangePassword(PasswordHasher.Hash(temporaryPassword), mustChangePassword: true, now);
        await db.SaveChangesAsync(cancellationToken);
        await Sessions.CloseAsync(db, user.Id, now, cancellationToken);
        return new UserInvitationDto(await GetUserHandler.ListItemAsync(db, mapper, user.Id, cancellationToken), temporaryPassword);
    }
}
