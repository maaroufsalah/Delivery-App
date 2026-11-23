using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Application.Common.Security;

/// <summary>`common/scope/request-scope.ts` : la portée se lit sur l'appelant.</summary>
public static class Scope
{
    public const string DenyAllId = "__scope_denies_all__";

    public static bool HasRole(this ICurrentUser user, RoleName role) => user.Roles.Contains(role);

    public static bool IsSuperAdmin(this ICurrentUser user) => user.HasRole(RoleName.SUPER_ADMIN);

    public static bool MayFilterBy(this ICurrentUser user, string? pinned) => user.IsSuperAdmin() || pinned is null;

    public static bool CanSeeInternalCash(this ICurrentUser user) =>
        user.ClientId is null && (user.IsSuperAdmin() || user.HasRole(RoleName.STAFF) || user.HasRole(RoleName.COURIER));

    public static bool CanSeeInternalNotes(this ICurrentUser user) => user.CanSeeInternalCash();

    public static bool CanSeeCourierIdentity(this ICurrentUser user) => user.CanSeeInternalCash();

    public static bool CanSeeInternalPricing(this ICurrentUser user) =>
        user.ClientId is null && user.CourierId is null && (user.IsSuperAdmin() || user.HasRole(RoleName.STAFF));

    /// <summary>
    /// `request.scope` absent : pas de société dans le jeton. Speedy refuse alors
    /// toute route qui lit la portée.
    /// </summary>
    public static void EnsureScope(this ICurrentUser user)
    {
        if (!user.IsAuthenticated)
        {
            throw BusinessException.Unauthenticated("Portée absente : cette route exige une authentification.");
        }
        if (user.CompanyId is null)
        {
            throw BusinessException.Forbidden("Un administrateur de plateforme n’a pas de société : utilisez « se connecter en tant que ».");
        }
    }
}
