using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Parcels;
using Delivery.Application.Staff;
using Delivery.Domain.Common;
using Delivery.Domain.Parcels;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Dashboard;

/// <summary>`GET /dashboard` : KPI colis et période précédente, courbe par famille, villes, derniers colis, alertes.</summary>
public sealed record GetDashboardQuery(DashboardFilters Filters) : IRequest<DashboardOverviewDto>, IDashboardRequest;

public sealed class GetDashboardValidator : DashboardValidator<GetDashboardQuery>;

internal sealed class GetDashboardHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IMapper mapper) : IRequestHandler<GetDashboardQuery, DashboardOverviewDto>
{
    public async Task<DashboardOverviewDto> Handle(GetDashboardQuery request, CancellationToken cancellationToken)
    {
        var ctx = await DashboardScope.ResolveAsync(db, caller, clock, request.Filters, cancellationToken);
        var (win, prev) = (ctx.Win, ctx.Prev);
        var startOfToday = Periods.ZonedDayStart(Periods.LocalDay(clock.UtcNow, ctx.Zone), ctx.Zone);
        var all = DashboardScope.Parcels(db, ctx.Base);
        var inWindow = DashboardScope.Parcels(db, ctx.Population);

        var todayCount = await all.CountAsync(p => p.CreatedAt >= startOfToday, cancellationToken);
        var byStatus = await StatusCountsAsync(inWindow, cancellationToken);
        var prevByStatus = ctx.PreviousPopulation is { } previousPopulation ? await StatusCountsAsync(DashboardScope.Parcels(db, previousPopulation), cancellationToken) : null;
        var buckets = Periods.Buckets(win, ctx.Zone);
        var dayRows = await DashboardSql.BucketRowsAsync(db, ctx.Population, buckets, cancellationToken);
        var cityRows = await inWindow.GroupBy(p => p.DestinationCityId).Select(g => new { CityId = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count).Take(8).ToListAsync(cancellationToken);
        var codToRemit = await CodToRemitAsync(ctx.Population, cancellationToken);
        var agency = request.Filters.Clean().AgencyId is { } wanted && (ctx.SuperAdmin || wanted == caller.AgencyId) ? wanted : caller.AgencyId;
        var stuck = await StaffAlertQueries.StuckInRetryAsync(db, agency, null, cancellationToken);
        var stale = await StaffAlertQueries.StaleRoutesAsync(db, agency, clock.UtcNow, null, cancellationToken);
        var latestRows = await mapper.ProjectTo<ParcelRow>(all.OrderByDescending(p => p.StatusAt).Take(10)).ToListAsync(cancellationToken);

        var daily = buckets.Select(b => new DashboardDayPointDto { Date = b.Date }).ToList();
        foreach (var row in dayRows)
        {
            var point = daily[DashboardSql.BucketIndex(row.Bucket, buckets.Count)];
            switch (StatusCatalog.GroupOf(row.StatusId))
            {
                case "initial": point.Initial += row.Count; break;
                case "transit": point.Transit += row.Count; break;
                case "pivot": point.Pivot += row.Count; break;
                case "retry": point.Retry += row.Count; break;
                case "failure": point.Failure += row.Count; break;
                case "return": point.Return += row.Count; break;
                case "terminal": point.Terminal += row.Count; break;
            }
        }
        var cityIds = cityRows.Select(r => r.CityId).ToList();
        var cityNames = await db.Cities.AsNoTracking().Where(c => cityIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        var byCity = cityRows.Select(r => new DashboardCityPointDto(r.CityId, cityNames.GetValueOrDefault(r.CityId, r.CityId), r.Count)).ToList();
        var counts = DashboardScope.Counts(byStatus);
        return new DashboardOverviewDto(
            ctx.WindowDto(),
            prev is null ? null : new DashboardPreviousWindowDto(prev.From, prev.To),
            new DashboardKpisDto(counts.Total, counts.Delivered, counts.InProgress, counts.InTransit, counts.Returned, counts.Failed, counts.Closed, counts.SuccessRate,
                counts.ReturnRate, todayCount, counts.Delivered, codToRemit),
            prevByStatus is null ? null : DashboardScope.Counts(prevByStatus),
            daily,
            byCity,
            latestRows.Select(row => ParcelViews.ToListItem(row, caller)).ToList(),
            new DashboardOverviewAlertsDto(stuck.Items, stale.Items));
    }

    private static async Task<List<(int StatusId, int Count)>> StatusCountsAsync(IQueryable<Domain.Entities.Parcel> parcels, CancellationToken cancellationToken) =>
        (await parcels.GroupBy(p => p.StatusId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(cancellationToken)).Select(r => (r.Key, r.Count)).ToList();

    /// <summary>`codToRemit` : le COD des colis livrés encaissés, remis ou facturés, pas encore reversés.</summary>
    private async Task<long> CodToRemitAsync(ParcelPopulation pop, CancellationToken cancellationToken)
    {
        var sql = new DashboardSqlText();
        var rows = await sql.RunAsync<long>(db, $"""
            WITH pop AS (
              SELECT {DashboardSql.PopColumns} FROM parcels p
              WHERE {DashboardSql.Population(sql, pop)}
                AND p."statusId" = {DashboardSql.Delivered}
                AND p."financialStatus" IN ('ENCAISSE', 'REMIS_LIVREUR', 'FACTURE')
            ){DashboardSql.CollectedCte}
            SELECT {DashboardSql.SumCents(DashboardSql.Collected)} AS "Value"
            FROM pop p {DashboardSql.CollectedJoin}
            """, cancellationToken);
        return rows.FirstOrDefault();
    }
}
