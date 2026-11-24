using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Parcels;

/// <summary>`parcel-lookup.ts` : un colis par son code, sinon par la référence du vendeur.</summary>
public static class ParcelLookup
{
    /// <summary>`resolveScannedParcel` : le code d'abord ; la référence ensuite, refusée si ambiguë.</summary>
    public static async Task<T?> ResolveAsync<T>(IQueryable<Parcel> scoped, string raw, Func<IQueryable<Parcel>, Task<T?>> read)
        where T : class
    {
        var trimmed = raw.Trim();
        if (trimmed.Length == 0) return null;
        var code = trimmed.ToUpperInvariant();
        var byCode = await read(scoped.Where(p => p.Code == code));
        if (byCode is not null) return byCode;
        var ids = await scoped.Where(p => p.ExternalCode != null && p.ExternalCode.ToLower() == trimmed.ToLower()).Select(p => p.Id).Take(2).ToListAsync();
        if (ids.Count == 0) return null;
        if (ids.Count > 1) throw BusinessException.Validation("Plusieurs colis portent cette référence : scannez le code colis.", new { reason = "ambiguous-reference" });
        var id = ids[0];
        return await read(scoped.Where(p => p.Id == id));
    }

    /// <summary>`assertExternalCodeFree` : une référence par vendeur, sans tenir compte de la casse.</summary>
    public static async Task AssertExternalCodeFreeAsync(IQueryable<Parcel> parcels, string clientId, string? externalCode, string? exceptParcelId, CancellationToken cancellationToken)
    {
        var reference = externalCode?.Trim();
        if (string.IsNullOrEmpty(reference)) return;
        var taken = await parcels.AnyAsync(p => p.ClientId == clientId && p.DeletedAt == null && p.ExternalCode != null
                                                && p.ExternalCode.ToLower() == reference.ToLower() && (exceptParcelId == null || p.Id != exceptParcelId), cancellationToken);
        if (taken) throw ExternalCodeTaken(reference);
    }

    public static BusinessException ExternalCodeTaken(string reference) =>
        BusinessException.Validation($"Référence déjà utilisée : « {reference} ».", new { field = "externalCode", reason = "external-code-taken", externalCode = reference });
}
