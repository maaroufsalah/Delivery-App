using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Roles.Dtos;
using Delivery.Domain.Enums;
using Delivery.Domain.Identity;
using MediatR;

namespace Delivery.Application.Roles.Queries;

/// <summary>`GET /roles` (et `POST /roles/reload`, qui ne vide qu'un cache) : la matrice des droits.</summary>
public sealed record GetRoleMatrixQuery : IRequest<RoleMatrixDto>;

internal sealed class GetRoleMatrixHandler(IAppDbContext db) : IRequestHandler<GetRoleMatrixQuery, RoleMatrixDto>
{
    public static readonly IReadOnlyList<RoleName> TenantRoles = [RoleName.SUPER_ADMIN, RoleName.STAFF, RoleName.MERCHANT, RoleName.COURIER];

    public static IReadOnlyList<PermissionDto> TenantPermissions { get; } =
        PermissionCatalog.All.Where(p => p.Module != "platform").Select(p => new PermissionDto(p.Key, p.Module, p.Label)).ToList();

    public Task<RoleMatrixDto> Handle(GetRoleMatrixQuery request, CancellationToken cancellationToken) => BuildAsync(db, cancellationToken);

    internal static async Task<RoleMatrixDto> BuildAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var matrix = await db.RoleMatrixAsync(cancellationToken);
        var granted = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var role in TenantRoles)
        {
            granted[role.ToString()] = matrix.TryGetValue(role, out var keys)
                ? keys.Where(k => !k.StartsWith("platform.", StringComparison.Ordinal)).ToList()
                : [];
        }
        return new RoleMatrixDto(TenantRoles.Select(r => r.ToString()).ToList(), TenantPermissions, granted);
    }
}
