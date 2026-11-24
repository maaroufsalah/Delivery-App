using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Couriers;

internal static class CourierChecks
{
    public static async Task AssertCityExistsAsync(IAppDbContext db, string cityId, CancellationToken cancellationToken)
    {
        if (!await db.Cities.AnyAsync(c => c.Id == cityId && c.DeletedAt == null, cancellationToken)) throw BusinessException.Validation("Ville inconnue.", new { field = "cityId" });
    }

    public static async Task AssertAgencyExistsAsync(IAppDbContext db, string agencyId, CancellationToken cancellationToken)
    {
        if (!await db.Agencies.AnyAsync(a => a.Id == agencyId && a.DeletedAt == null, cancellationToken)) throw BusinessException.Validation("Agence inconnue.", new { field = "agencyId" });
    }
}
