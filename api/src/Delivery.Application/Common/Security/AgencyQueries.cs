using Delivery.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Common.Security;

/// <summary>`AgencyResolverService` : l'agence qui sert une ville.</summary>
public static class AgencyQueries
{
    /// <summary>L'agence qui couvre la ville, sinon celle qui y est, sinon le siège (la plus ancienne).</summary>
    public static async Task<string?> AgencyForCityAsync(this IAppDbContext db, string? cityId, CancellationToken cancellationToken)
    {
        if (cityId is not null)
        {
            var covering = await db.Agencies.AsNoTracking()
                .Where(a => a.DeletedAt == null && a.IsActive && a.CityLinks.Any(l => l.CityId == cityId))
                .OrderBy(a => a.CreatedAt).ThenBy(a => a.Id)
                .Select(a => a.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (covering is not null) return covering;
            var local = await db.Agencies.AsNoTracking()
                .Where(a => a.DeletedAt == null && a.IsActive && a.CityId == cityId)
                .OrderBy(a => a.CreatedAt).ThenBy(a => a.Id)
                .Select(a => a.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (local is not null) return local;
        }
        return await db.Agencies.AsNoTracking()
            .Where(a => a.DeletedAt == null)
            .OrderBy(a => a.CreatedAt).ThenBy(a => a.Id)
            .Select(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public static Task<string?> AgencyForCourierAsync(this IAppDbContext db, string? agencyId, string? cityId, CancellationToken cancellationToken) =>
        agencyId is not null ? Task.FromResult<string?>(agencyId) : db.AgencyForCityAsync(cityId, cancellationToken);
}
