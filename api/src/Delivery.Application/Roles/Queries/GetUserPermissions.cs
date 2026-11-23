using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Roles.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Roles.Queries;

/// <summary>`GET /users/:id/permissions` : droits du rôle, écarts et résultat effectif.</summary>
public sealed record GetUserPermissionsQuery(string UserId) : IRequest<UserPermissionsDto>;

internal sealed class GetUserPermissionsHandler(IAppDbContext db) : IRequestHandler<GetUserPermissionsQuery, UserPermissionsDto>
{
    public Task<UserPermissionsDto> Handle(GetUserPermissionsQuery request, CancellationToken cancellationToken) => BuildAsync(db, request.UserId, cancellationToken);

    internal static async Task<List<RoleName>> RolesOfAsync(IAppDbContext db, string userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.DeletedAt == null)
            .Select(u => new { Roles = u.Roles.Select(r => r.Role).ToList() })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw BusinessException.NotFound("Compte introuvable.");
        return user.Roles;
    }

    internal static async Task<UserPermissionsDto> BuildAsync(IAppDbContext db, string userId, CancellationToken cancellationToken)
    {
        var roles = await RolesOfAsync(db, userId, cancellationToken);
        var fromRoles = await db.PermissionsForRolesAsync(roles, cancellationToken);
        var effective = await db.EffectivePermissionsAsync(userId, roles, cancellationToken);
        var locked = roles.Contains(RoleName.SUPER_ADMIN);
        var mine = locked ? null : await db.OverridesOfAsync(userId, cancellationToken);
        static List<string> Tenant(IEnumerable<string> keys) => keys.Where(k => !k.StartsWith("platform.", StringComparison.Ordinal)).ToList();
        return new UserPermissionsDto(
            userId,
            roles.Select(r => r.ToString()).ToList(),
            locked,
            GetRoleMatrixHandler.TenantPermissions,
            Tenant(fromRoles),
            Tenant(mine?.Grants ?? new HashSet<string>()),
            Tenant(mine?.Revokes ?? new HashSet<string>()),
            Tenant(effective));
    }
}
