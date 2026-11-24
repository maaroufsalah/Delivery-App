using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using MediatR;

namespace Delivery.Application.Dashboard;

/// <summary>`GET /dashboard/delays` : délai ramassé → livré (moyen, médian, parts &lt; 24 h, 24-48 h, &gt; 48 h) et sa courbe.</summary>
public sealed record GetDashboardDelaysQuery(DashboardFilters Filters) : IRequest<DashboardDelaysDto>, IDashboardRequest;

public sealed class GetDashboardDelaysValidator : DashboardValidator<GetDashboardDelaysQuery>;

public sealed class DashboardDelayAggRow
{
    public int Delivered { get; set; }
    public int Measured { get; set; }
    public double? AvgHours { get; set; }
    public double? MedianHours { get; set; }
    public int Within24 { get; set; }
    public int Within48 { get; set; }
}

public sealed class DashboardDelayBucketRow
{
    public int Bucket { get; set; }
    public double TotalHours { get; set; }
    public int Count { get; set; }
}

internal sealed class GetDashboardDelaysHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetDashboardDelaysQuery, DashboardDelaysDto>
{
    public async Task<DashboardDelaysDto> Handle(GetDashboardDelaysQuery request, CancellationToken cancellationToken)
    {
        var ctx = await DashboardScope.ResolveAsync(db, caller, clock, request.Filters, cancellationToken);
        var buckets = Periods.Buckets(ctx.Win, ctx.Zone);
        var agg = await AggAsync(ctx.Population, cancellationToken);
        var seriesRows = await SeriesAsync(ctx.Population, buckets, cancellationToken);
        var prev = ctx.PreviousPopulation is { } previousPopulation ? await AggAsync(previousPopulation, cancellationToken) : null;

        var series = buckets.Select(b => new DashboardDelayPointDto { Date = b.Date }).ToList();
        var sums = new double[buckets.Count];
        foreach (var row in seriesRows)
        {
            var at = DashboardSql.BucketIndex(row.Bucket, buckets.Count);
            sums[at] += row.TotalHours;
            series[at].Count += row.Count;
        }
        for (var i = 0; i < series.Count; i++) series[i].AvgHours = series[i].Count == 0 ? null : sums[i] / series[i].Count;

        var shares = Shares(agg);
        return new DashboardDelaysDto(agg.Delivered, agg.Measured, agg.Delivered - agg.Measured, agg.AvgHours, agg.MedianHours, shares.Within24, shares.Within48, shares.Over48,
            ctx.Win.Granularity, series, prev is null ? null : Previous(prev));
    }

    private static DashboardDelaysPreviousDto Previous(DashboardDelayAggRow prev)
    {
        var shares = Shares(prev);
        return new DashboardDelaysPreviousDto(prev.AvgHours, shares.Within24, shares.Within48, shares.Over48);
    }

    /// <summary>`shares` : « within48 » est la tranche 24-48 h, pas le cumul.</summary>
    private static (double? Within24, double? Within48, double? Over48) Shares(DashboardDelayAggRow value) =>
        (DashboardScope.Ratio(value.Within24, value.Measured), DashboardScope.Ratio(value.Within48 - value.Within24, value.Measured),
            DashboardScope.Ratio(value.Measured - value.Within48, value.Measured));

    private async Task<DashboardDelayAggRow> AggAsync(ParcelPopulation pop, CancellationToken cancellationToken)
    {
        var sql = new DashboardSqlText();
        var h = DashboardSql.DelayHours;
        var rows = await sql.RunAsync<DashboardDelayAggRow>(db, $"""
            WITH pop AS (
              SELECT {DashboardSql.PopColumns} FROM parcels p
              WHERE {DashboardSql.Population(sql, pop)}
                AND p."statusId" = {DashboardSql.Delivered}
            ){DashboardSql.DelayCte}
            SELECT COUNT(*)::int AS "Delivered",
                   COUNT({h})::int AS "Measured",
                   AVG({h})::float8 AS "AvgHours",
                   percentile_cont(0.5) WITHIN GROUP (ORDER BY {h})::float8 AS "MedianHours",
                   COUNT(*) FILTER (WHERE {h} <= 24)::int AS "Within24",
                   COUNT(*) FILTER (WHERE {h} <= 48)::int AS "Within48"
            FROM pop p {DashboardSql.DelayJoin}
            """, cancellationToken);
        return rows.FirstOrDefault() ?? new DashboardDelayAggRow();
    }

    private Task<List<DashboardDelayBucketRow>> SeriesAsync(ParcelPopulation pop, IReadOnlyList<(string Date, DateTime Start)> buckets, CancellationToken cancellationToken)
    {
        var sql = new DashboardSqlText();
        var starts = sql.P(buckets.Select(b => b.Start).ToArray());
        var h = DashboardSql.DelayHours;
        return sql.RunAsync<DashboardDelayBucketRow>(db, $"""
            WITH pop AS (
              SELECT {DashboardSql.PopColumns} FROM parcels p
              WHERE {DashboardSql.Population(sql, pop)}
                AND p."statusId" = {DashboardSql.Delivered}
            ){DashboardSql.DelayCte}
            SELECT width_bucket(p."createdAt", {starts}::timestamptz[]) AS "Bucket",
                   COALESCE(SUM({h}), 0)::float8 AS "TotalHours",
                   COUNT({h})::int AS "Count"
            FROM pop p {DashboardSql.DelayJoin}
            GROUP BY 1
            """, cancellationToken);
    }
}
