using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Roles.Dtos;
using Delivery.Application.Roles.Queries;
using Delivery.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Roles.Commands;

/// <summary>`DELETE /users/:id/permissions` : le compte revient aux seuls droits de son rôle.</summary>
public sealed record ResetUserPermissionsCommand(string UserId) : IRequest<UserPermissionsDto>;

internal sealed class ResetUserPermissionsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<ResetUserPermissionsCommand, UserPermissionsDto>
{
    public async Task<UserPermissionsDto> Handle(ResetUserPermissionsCommand request, CancellationToken cancellationToken)
    {
        var roles = await GetUserPermissionsHandler.RolesOfAsync(db, request.UserId, cancellationToken);
        UpdateUserPermissionsHandler.AssertEditable(roles);
        if (request.UserId == caller.UserId)
        {
            var fromRoles = await db.PermissionsForRolesAsync(roles, cancellationToken);
            if (!fromRoles.Contains("accounts.write"))
            {
                throw BusinessException.Validation(
                    "Votre rôle ne vous donne pas le droit de gérer les comptes : la réinitialisation vous le retirerait.",
                    new { field = "effective" });
            }
        }
        await db.UserPermissionOverrides.Where(o => o.UserId == request.UserId).ExecuteDeleteAsync(cancellationToken);
        return await GetUserPermissionsHandler.BuildAsync(db, request.UserId, cancellationToken);
    }
}
