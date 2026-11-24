using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Staff;

/// <summary>`StaffAlertsService.stuckInRetry` et `staleRoutes` : partagés par `/staff/me/alerts` et `/dashboard`.</summary>
public static class StaffAlertQueries
{
    public const int StaleRouteHours = 48;
    public const int RetryAlertThreshold = 2;

    /// <summary>Colis passés plus de deux fois par une relance ; <paramref name="limit"/> nul : tous (le tableau de bord n'en passe pas).</summary>
    public static async Task<StaffAlertGroupDto<StuckParcelAlertDto>> StuckInRetryAsync(IAppDbContext db, string? agency, int? limit, CancellationToken cancellationToken)
    {
        var rows = await db.ParcelEvents.AsNoTracking().Where(e => e.Status.Group == ParcelStatusGroup.RETRY).GroupBy(e => e.ParcelId)
            .Select(g => new { ParcelId = g.Key, Count = g.Count() }).Where(g => g.Count > RetryAlertThreshold).ToListAsync(cancellationToken);
        if (rows.Count == 0) return new StaffAlertGroupDto<StuckParcelAlertDto>(0, []);
        var attempts = rows.ToDictionary(r => r.ParcelId, r => r.Count);
        var ids = attempts.Keys.ToList();
        var parcels = db.Parcels.AsNoTracking().Where(p => p.DeletedAt == null && ids.Contains(p.Id));
        if (agency is not null) parcels = parcels.Where(p => p.AgencyId == agency);
        var items = (await parcels.Select(p => new { p.Id, p.Code, p.RecipientName, p.StatusId }).ToListAsync(cancellationToken))
            .Select(p => new StuckParcelAlertDto(p.Code, p.RecipientName, attempts.GetValueOrDefault(p.Id), p.StatusId)).OrderByDescending(p => p.Attempts).ToList();
        return new StaffAlertGroupDto<StuckParcelAlertDto>(items.Count, limit is { } take ? items.Take(take).ToList() : items);
    }

    /// <summary>Circuits ouverts depuis plus de 48 h ; <paramref name="limit"/> nul : tous.</summary>
    public static async Task<StaffAlertGroupDto<StaleRouteAlertDto>> StaleRoutesAsync(IAppDbContext db, string? agency, DateTime now, int? limit, CancellationToken cancellationToken)
    {
        var before = now.AddHours(-StaleRouteHours);
        var routes = db.Routes.AsNoTracking().Where(r => r.Status != RouteStatus.CLOSED && r.CreatedAt < before);
        if (agency is not null) routes = routes.Where(r => r.Courier.AgencyId == agency);
        var count = await routes.CountAsync(cancellationToken);
        var ordered = routes.OrderBy(r => r.CreatedAt);
        var page = limit is { } take ? ordered.Take(take) : ordered;
        var rows = await page.Select(r => new { r.Code, r.Courier.FirstName, r.Courier.LastName, City = r.City.Name, r.CreatedAt }).ToListAsync(cancellationToken);
        return new StaffAlertGroupDto<StaleRouteAlertDto>(count,
            rows.Select(r => new StaleRouteAlertDto(r.Code, $"{r.FirstName} {r.LastName}".Trim(), r.City, (long)Math.Round((now - r.CreatedAt).TotalHours, MidpointRounding.AwayFromZero))).ToList());
    }
}
