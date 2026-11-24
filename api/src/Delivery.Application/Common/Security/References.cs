using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;

namespace Delivery.Application.Common.Security;

/// <summary>`ReferenceCount` : combien de lignes retiennent encore une fiche.</summary>
public sealed record ReferenceCount(string Entity, int Count);

public static class References
{
    /// <summary>`assertDeletable` : 409 listant les références bloquantes.</summary>
    public static void AssertDeletable(string label, params ReferenceCount[] counts)
    {
        var blocking = counts.Where(c => c.Count > 0).ToList();
        if (blocking.Count == 0) return;
        throw BusinessException.Conflict($"Suppression refusée : {label} est encore référencé(e).", new { references = blocking });
    }

    public static ReferenceCount Of(string entity, int count) => new(entity, count);

    /// <summary>`assertCanSeeInternalPricing` : la grille du transporteur reste interne.</summary>
    public static void AssertCanSeeInternalPricing(this ICurrentUser user, string what)
    {
        if (user.CanSeeInternalPricing()) return;
        throw BusinessException.Forbidden($"{what} : cette grille appartient au transporteur et ne se lit pas depuis un compte e-commerçant ou livreur.");
    }
}
