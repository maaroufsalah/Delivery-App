using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Staff;
using Delivery.Domain.Common;
using Delivery.Domain.Parcels;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Dashboard;

/// <summary>`GET /dashboard/alerts` : sept familles d'alertes aux seuils réglables ; les deux financières exigent `finance.read`.</summary>
public sealed record GetDashboardAlertsQuery(DashboardFilters Filters) : IRequest<DashboardAlertsDto>, IDashboardRequest;

public sealed class GetDashboardAlertsValidator : DashboardValidator<GetDashboardAlertsQuery>;

public sealed class DashboardRateRow
{
    public string Id { get; set; } = string.Empty;
    public int Delivered { get; set; }
    public int Returned { get; set; }
    public int Failed { get; set; }
    public int Total { get; set; }
}

public sealed class DashboardCodLateRow
{
    public string Id { get; set; } = string.Empty;
    public int Count { get; set; }
    public long Amount { get; set; }
    public DateTime Oldest { get; set; }
}

internal sealed class GetDashboardAlertsHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetDashboardAlertsQuery, DashboardAlertsDto>
{
    private const int Sample = 5;

    public async Task<DashboardAlertsDto> Handle(GetDashboardAlertsQuery request, CancellationToken cancellationToken)
    {
        var ctx = await DashboardScope.ResolveAsync(db, caller, clock, request.Filters, cancellationToken);
        var thresholds = await DashboardThresholds.LoadAsync(db, ctx.Population.AgencyId, cancellationToken);
        var granted = await db.EffectivePermissionsAsync(caller.UserId, caller.Roles, cancellationToken);
        var finance = caller.IsSuperAdmin() || granted.Contains("finance.read");
        var state = ctx.Population.Unbounded();
        var now = clock.UtcNow;

        var courierRates = await RatesAsync(ctx.Population, DashboardRankings.CourierDimension, cancellationToken);
        var cityRates = await RatesAsync(ctx.Population, DashboardRankings.CityDimension, cancellationToken);
        var previousRates = ctx.PreviousPopulation is { } previousPopulation ? await RatesAsync(previousPopulation, DashboardRankings.CourierDimension, cancellationToken) : [];
        var stale = await DashboardOps.StaleDeliveryRoutesAsync(db, ctx, now, StaffAlertQueries.StaleRouteHours, Sample, cancellationToken);
        var stuck = await StuckParcelsAsync(state, thresholds, now, cancellationToken);
        var codNotRemitted = finance ? await CodNotRemittedAsync(state, thresholds, now, cancellationToken) : null;
        var variances = finance ? await CashVariancesAsync(ctx, cancellationToken) : null;

        return new DashboardAlertsDto
        {
            CourierReturnRate = await CourierReturnRateAsync(courierRates, thresholds, cancellationToken),
            CodNotRemitted = codNotRemitted,
            StaleRoutes = stale,
            StuckParcels = stuck,
            CashVariances = variances,
            PerformanceDrops = await PerformanceDropsAsync(courierRates, previousRates, thresholds, cancellationToken),
            CityFailureRate = await CityFailureRateAsync(cityRates, thresholds, cancellationToken),
            Thresholds = thresholds,
            GeneratedAt = now,
        };
    }

    private Task<List<DashboardRateRow>> RatesAsync(ParcelPopulation pop, string dimension, CancellationToken cancellationToken)
    {
        var sql = new DashboardSqlText();
        return sql.RunAsync<DashboardRateRow>(db, $"""
            SELECT {dimension} AS "Id",
                   {DashboardSql.CountDelivered} AS "Delivered",
                   {DashboardSql.CountReturned} AS "Returned",
                   {DashboardSql.CountFailed} AS "Failed",
                   COUNT(*)::int AS "Total"
            FROM parcels p
            WHERE {DashboardSql.Population(sql, pop)} AND {dimension} IS NOT NULL
            GROUP BY 1
            """, cancellationToken);
    }

    /// <summary>Taux de retour d'un livreur au-dessus du seuil, sur au moins `minParcels` colis clos.</summary>
    private async Task<StaffAlertGroupDto<CourierRateAlertDto>> CourierReturnRateAsync(List<DashboardRateRow> rows, DashboardThresholdsDto thresholds, CancellationToken cancellationToken)
    {
        var offenders = rows.Select(r => (Row: r, Closed: r.Delivered + r.Returned)).Where(x => x.Closed >= thresholds.MinParcels)
            .Select(x => (x.Row, Rate: (double)x.Row.Returned / x.Closed)).Where(x => x.Rate > thresholds.CourierReturnRate).OrderByDescending(x => x.Rate).ToList();
        var sample = offenders.Take(Sample).ToList();
        var names = await DashboardRankings.CourierNamesAsync(db, sample.Select(x => x.Row.Id).ToList(), cancellationToken);
        return new StaffAlertGroupDto<CourierRateAlertDto>(offenders.Count,
            sample.Select(x => new CourierRateAlertDto(x.Row.Id, names.GetValueOrDefault(x.Row.Id, "-"), x.Row.Delivered, x.Row.Returned, x.Rate)).ToList());
    }

    /// <summary>Chute du taux de réussite d'un livreur d'une période à l'autre, au-delà de `performanceDropPts`.</summary>
    private async Task<StaffAlertGroupDto<PerformanceDropAlertDto>> PerformanceDropsAsync(List<DashboardRateRow> current, List<DashboardRateRow> previous, DashboardThresholdsDto thresholds,
        CancellationToken cancellationToken)
    {
        var before = previous.ToDictionary(r => r.Id);
        var drops = new List<(string Id, double CurrentRate, double PreviousRate)>();
        foreach (var row in current)
        {
            var closedNow = row.Delivered + row.Returned;
            if (!before.TryGetValue(row.Id, out var prev)) continue;
            var closedBefore = prev.Delivered + prev.Returned;
            if (closedNow < thresholds.MinParcels || closedBefore < thresholds.MinParcels) continue;
            var currentRate = (double)row.Delivered / closedNow;
            var previousRate = (double)prev.Delivered / closedBefore;
            if (previousRate - currentRate > thresholds.PerformanceDropPts) drops.Add((row.Id, currentRate, previousRate));
        }
        drops = drops.OrderByDescending(x => x.PreviousRate - x.CurrentRate).ToList();
        var sample = drops.Take(Sample).ToList();
        var names = await DashboardRankings.CourierNamesAsync(db, sample.Select(x => x.Id).ToList(), cancellationToken);
        return new StaffAlertGroupDto<PerformanceDropAlertDto>(drops.Count,
            sample.Select(x => new PerformanceDropAlertDto(x.Id, names.GetValueOrDefault(x.Id, "-"), x.CurrentRate, x.PreviousRate)).ToList());
    }

    /// <summary>Villes dont la part d'échecs dépasse `cityFailureRate`, sur au moins `minParcels` colis.</summary>
    private async Task<StaffAlertGroupDto<CityFailureAlertDto>> CityFailureRateAsync(List<DashboardRateRow> rows, DashboardThresholdsDto thresholds, CancellationToken cancellationToken)
    {
        var offenders = rows.Where(r => r.Total >= thresholds.MinParcels).Select(r => (Row: r, Rate: (double)r.Failed / r.Total))
            .Where(x => x.Rate > thresholds.CityFailureRate).OrderByDescending(x => x.Rate).ToList();
        var sample = offenders.Take(Sample).ToList();
        var ids = sample.Select(x => x.Row.Id).ToList();
        var names = await db.Cities.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        return new StaffAlertGroupDto<CityFailureAlertDto>(offenders.Count,
            sample.Select(x => new CityFailureAlertDto(x.Row.Id, names.GetValueOrDefault(x.Row.Id, x.Row.Id), x.Row.Total, x.Row.Failed, x.Rate)).ToList());
    }

    /// <summary>Colis « en cours » sans changement de statut depuis `stuckParcelDays` jours (l'état, sans période).</summary>
    private async Task<StaffAlertGroupDto<StuckParcelAgeAlertDto>> StuckParcelsAsync(ParcelPopulation pop, DashboardThresholdsDto thresholds, DateTime now, CancellationToken cancellationToken)
    {
        var before = now.AddMilliseconds(-thresholds.StuckParcelDays * 86_400_000);
        var inProgress = StatusCatalog.DashboardInProgressStatusIds;
        var parcels = DashboardScope.Parcels(db, pop).Where(p => inProgress.Contains(p.StatusId) && p.StatusAt < before);
        var count = await parcels.CountAsync(cancellationToken);
        var rows = await parcels.OrderBy(p => p.StatusAt).Take(Sample).Select(p => new { p.Code, p.RecipientName, p.StatusId, p.StatusAt }).ToListAsync(cancellationToken);
        return new StaffAlertGroupDto<StuckParcelAgeAlertDto>(count,
            rows.Select(r => new StuckParcelAgeAlertDto(r.Code, r.RecipientName, r.StatusId, (long)Math.Floor((now - r.StatusAt).TotalMilliseconds / 86_400_000))).ToList());
    }

    /// <summary>COD encaissé, livré depuis plus de `codLateHours` heures et toujours chez le livreur, par livreur (l'état, sans période).</summary>
    private async Task<CodNotRemittedGroupDto> CodNotRemittedAsync(ParcelPopulation pop, DashboardThresholdsDto thresholds, DateTime now, CancellationToken cancellationToken)
    {
        var sql = new DashboardSqlText();
        var before = now.AddMilliseconds(-thresholds.CodLateHours * 3_600_000);
        var rows = await sql.RunAsync<DashboardCodLateRow>(db, $"""
            WITH pop AS (
              SELECT {DashboardSql.PopColumns} FROM parcels p
              WHERE {DashboardSql.Population(sql, pop)}
                AND p."statusId" = {DashboardSql.Delivered} AND p."financialStatus" = 'ENCAISSE'
                AND p."courierId" IS NOT NULL
            ){DashboardSql.CollectedCte}{DashboardSql.DelayCte}
            SELECT p."courierId" AS "Id",
                   COUNT(*)::int AS "Count",
                   {DashboardSql.SumCents(DashboardSql.Collected)} AS "Amount",
                   MIN(ev.delivered_at) AS "Oldest"
            FROM pop p {DashboardSql.CollectedJoin} {DashboardSql.DelayJoin}
            WHERE ev.delivered_at IS NOT NULL AND ev.delivered_at < {sql.P(before)}
            GROUP BY 1
            ORDER BY MIN(ev.delivered_at) ASC
            """, cancellationToken);
        var sample = rows.Take(Sample).ToList();
        var names = await DashboardRankings.CourierNamesAsync(db, sample.Select(r => r.Id).ToList(), cancellationToken);
        return new CodNotRemittedGroupDto(rows.Sum(r => r.Count), rows.Sum(r => r.Amount),
            sample.Select(r => new CodNotRemittedAlertDto(r.Id, names.GetValueOrDefault(r.Id, "-"), r.Count, r.Amount,
                (long)Math.Floor((now - r.Oldest).TotalMilliseconds / 3_600_000 + 0.5))).ToList());
    }

    /// <summary>Écarts de caisse des RF- confirmés dans la fenêtre, les plus récents d'abord.</summary>
    private async Task<StaffAlertGroupDto<RemittanceAlertDto>> CashVariancesAsync(DashboardContext ctx, CancellationToken cancellationToken)
    {
        var remittances = DashboardOps.Variances(db, ctx, ctx.Win.Start, ctx.Win.End);
        var count = await remittances.CountAsync(cancellationToken);
        var rows = await remittances.OrderByDescending(r => r.ConfirmedAt).Take(Sample)
            .Select(r => new { r.Code, r.Courier.FirstName, r.Courier.LastName, City = r.City.Name, r.Amount, r.Variance, r.VarianceReason, r.ConfirmedAt, r.CreatedAt })
            .ToListAsync(cancellationToken);
        return new StaffAlertGroupDto<RemittanceAlertDto>(count, rows.Select(r => new RemittanceAlertDto(r.Code, $"{r.FirstName} {r.LastName}".Trim(), r.City, Money.ToCents(r.Amount),
            Money.ToCents(r.Variance), r.VarianceReason, r.ConfirmedAt ?? r.CreatedAt)).ToList());
    }
}
