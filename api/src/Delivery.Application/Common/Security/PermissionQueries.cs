using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Enums;
using Delivery.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Common.Security;

public sealed record UserOverrides(IReadOnlySet<string> Grants, IReadOnlySet<string> Revokes);

/// <summary>
/// `PermissionsService` : la matrice de la société (table `role_permissions`,
/// matrice par défaut tant qu'elle est vide) et les écarts par utilisateur.
/// Ensembles rendus dans l'ordre du catalogue.
/// </summary>
public static class PermissionQueries
{
    public static async Task<Dictionary<RoleName, SortedSet<string>>> RoleMatrixAsync(this IAppDbContext db, CancellationToken cancellationToken)
    {
        var rows = await db.RolePermissions.AsNoTracking().Select(r => new { r.Role, r.PermissionId }).ToListAsync(cancellationToken);
        var matrix = new Dictionary<RoleName, SortedSet<string>>();
        foreach (var row in rows.Where(r => PermissionCatalog.IsPermissionKey(r.PermissionId)))
        {
            if (!matrix.TryGetValue(row.Role, out var set)) matrix[row.Role] = set = NewSet();
            set.Add(row.PermissionId);
        }
        if (rows.Count == 0)
        {
            foreach (var (role, keys) in PermissionCatalog.DefaultRolePermissions)
            {
                var set = NewSet();
                set.UnionWith(keys);
                matrix[Enum.Parse<RoleName>(role)] = set;
            }
        }
        return matrix;
    }

    public static async Task<SortedSet<string>> PermissionsForRolesAsync(this IAppDbContext db, IEnumerable<RoleName> roles, CancellationToken cancellationToken)
    {
        var matrix = await db.RoleMatrixAsync(cancellationToken);
        var granted = NewSet();
        foreach (var role in roles)
        {
            if (matrix.TryGetValue(role, out var keys)) granted.UnionWith(keys);
        }
        return granted;
    }

    public static async Task<UserOverrides?> OverridesOfAsync(this IAppDbContext db, string userId, CancellationToken cancellationToken)
    {
        var rows = await db.UserPermissionOverrides.AsNoTracking()
            .Where(o => o.UserId == userId)
            .Select(o => new { o.PermissionId, o.Granted })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0) return null;
        var grants = NewSet();
        var revokes = NewSet();
        foreach (var row in rows.Where(r => PermissionCatalog.IsPermissionKey(r.PermissionId)))
        {
            (row.Granted ? grants : revokes).Add(row.PermissionId);
        }
        return new UserOverrides(grants, revokes);
    }

    public static async Task<SortedSet<string>> EffectivePermissionsAsync(this IAppDbContext db, string userId, IReadOnlyCollection<RoleName> roles, CancellationToken cancellationToken)
    {
        var granted = await db.PermissionsForRolesAsync(roles, cancellationToken);
        if (roles.Contains(RoleName.SUPER_ADMIN)) return granted;
        var mine = await db.OverridesOfAsync(userId, cancellationToken);
        if (mine is null) return granted;
        foreach (var key in mine.Grants.Where(k => !k.StartsWith("platform.", StringComparison.Ordinal))) granted.Add(key);
        foreach (var key in mine.Revokes) granted.Remove(key);
        return granted;
    }

    public static SortedSet<string> NewSet() => new(Comparer<string>.Create((a, b) =>
    {
        var order = PermissionCatalog.IndexOf(a).CompareTo(PermissionCatalog.IndexOf(b));
        return order != 0 ? order : string.CompareOrdinal(a, b);
    }));
}
