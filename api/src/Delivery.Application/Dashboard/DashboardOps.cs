using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Staff;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Dashboard;

/// <summary>`DashboardOpsService` (circuits) et les écarts de caisse, partagés par la finance et les alertes.</summary>
public static class DashboardOps
{
    /// <summary>`routeWhere` : circuits de livraison, filtres livreur / ville, agence du livreur.</summary>
    public static IQueryable<Route> Routes(IAppDbContext db, DashboardContext ctx)
    {
        var routes = db.Routes.AsNoTracking().Where(r => r.Kind == RouteKind.DELIVERY);
        if (ctx.Population.CourierId is { } courier) routes = routes.Where(r => r.CourierId == courier);
        if (ctx.Population.DestinationCityId is { } city) routes = routes.Where(r => r.CityId == city);
        if (ctx.CourierAgency is { } agency) routes = routes.Where(r => r.Courier.AgencyId == agency);
        if (ctx.Population.DenyAll) routes = routes.Where(r => r.Id == Scope.DenyAllId);
        return routes;
    }

    /// <summary>`openCount` : circuits non clôturés, créés avant <paramref name="before"/> s'il est donné.</summary>
    public static Task<int> OpenCountAsync(IQueryable<Route> routes, DateTime? before, CancellationToken cancellationToken)
    {
        var open = routes.Where(r => r.Status != RouteStatus.CLOSED);
        if (before is { } limit) open = open.Where(r => r.CreatedAt < limit);
        return open.CountAsync(cancellationToken);
    }

    /// <summary>`staleDeliveryRoutes` : circuits de livraison ouverts depuis plus de <paramref name="hours"/> heures.</summary>
    public static async Task<StaffAlertGroupDto<StaleRouteAlertDto>> StaleDeliveryRoutesAsync(IAppDbContext db, DashboardContext ctx, DateTime now, int hours, int limit,
        CancellationToken cancellationToken)
    {
        var routes = Routes(db, ctx);
        var before = now.AddHours(-hours);
        var count = await OpenCountAsync(routes, before, cancellationToken);
        var rows = await routes.Where(r => r.Status != RouteStatus.CLOSED && r.CreatedAt < before).OrderBy(r => r.CreatedAt).Take(limit)
            .Select(r => new { r.Code, r.Courier.FirstName, r.Courier.LastName, City = r.City.Name, r.CreatedAt }).ToListAsync(cancellationToken);
        return new StaffAlertGroupDto<StaleRouteAlertDto>(count, rows.Select(r => new StaleRouteAlertDto(r.Code, $"{r.FirstName} {r.LastName}".Trim(), r.City,
            (long)Math.Floor((now - r.CreatedAt).TotalMilliseconds / 3_600_000 + 0.5))).ToList());
    }

    /// <summary>Bons de remontée confirmés dans la fenêtre avec un écart non nul.</summary>
    public static IQueryable<CashRemittance> Variances(IAppDbContext db, DashboardContext ctx, DateTime from, DateTime to)
    {
        var remittances = db.CashRemittances.AsNoTracking()
            .Where(r => r.Status == CashRemittanceStatus.CONFIRMED && r.ConfirmedAt >= from && r.ConfirmedAt < to && r.Variance != 0);
        if (ctx.OwnAgency is { } agency) remittances = remittances.Where(r => r.AgencyId == agency);
        if (ctx.Population.CourierId is { } courier) remittances = remittances.Where(r => r.CourierId == courier);
        return remittances;
    }
}
