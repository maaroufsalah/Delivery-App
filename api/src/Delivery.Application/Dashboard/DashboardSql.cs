using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Delivery.Domain.Parcels;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Dashboard;

/// <summary>Un texte SQL et ses paramètres positionnels (`{0}`, `{1}`…), pour `SqlQueryRaw`.</summary>
public sealed class DashboardSqlText
{
    public List<object> Parameters { get; } = [];

    public string P(object value)
    {
        Parameters.Add(value);
        return $"{{{Parameters.Count - 1}}}";
    }

    public Task<List<T>> RunAsync<T>(IAppDbContext db, string text, CancellationToken cancellationToken) =>
        db.Database.SqlQueryRaw<T>(text, [.. Parameters]).ToListAsync(cancellationToken);
}

public sealed class DashboardBucketRow
{
    public int Bucket { get; set; }
    public int StatusId { get; set; }
    public int Count { get; set; }
}

public sealed class DashboardRankRow
{
    public string Id { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Delivered { get; set; }
    public int Returned { get; set; }
    public long Collected { get; set; }
    public double? AvgDelay { get; set; }
}

public sealed class DashboardCountRow
{
    public string Id { get; set; } = string.Empty;
    public int Value { get; set; }
}

public sealed class DashboardAmountRow
{
    public int Count { get; set; }
    public long Amount { get; set; }
}

/// <summary>`dashboard-sql.ts` : la population de colis et les expressions communes (sans `companyId` : mono-société).</summary>
public static class DashboardSql
{
    public const int Delivered = 4;

    public static string Ids(IEnumerable<int> ids) => string.Join(", ", ids);

    public const string PopColumns =
        "p.id, p.\"courierId\", p.\"clientId\", p.\"destinationCityId\", p.\"statusId\", p.\"financialStatus\", p.\"codAmount\", p.\"deliveryFee\", p.\"feeIncluded\", p.\"alreadyPaid\", p.\"createdAt\"";

    /// <summary>`populationSql`.</summary>
    public static string Population(DashboardSqlText sql, ParcelPopulation pop)
    {
        if (pop.DenyAll) return "FALSE";
        var parts = new List<string> { """p."deletedAt" IS NULL""" };
        if (pop.AgencyId is { } agency) parts.Add($"""p."agencyId" = {sql.P(agency)}""");
        if (pop.ClientId is { } client) parts.Add($"""p."clientId" = {sql.P(client)}""");
        if (pop.CourierId is { } courier) parts.Add($"""p."courierId" = {sql.P(courier)}""");
        if (pop.DestinationCityId is { } city) parts.Add($"""p."destinationCityId" = {sql.P(city)}""");
        if (pop.Start is { } start) parts.Add($"""p."createdAt" >= {sql.P(start)}""");
        if (pop.End is { } end) parts.Add($"""p."createdAt" < {sql.P(end)}""");
        return string.Join(" AND ", parts);
    }

    /// <summary>`COLLECTED_CTE` : la dernière course qui a livré le colis.</summary>
    public static readonly string CollectedCte = $"""
        , rp AS (
            SELECT DISTINCT ON (rp."parcelId") rp."parcelId", rp."codCollected", rp."codCash", rp."codCheque"
            FROM route_parcels rp
            JOIN pop ON pop.id = rp."parcelId" AND pop."statusId" = {Delivered}
            WHERE rp."finalStatusId" = {Delivered}
            ORDER BY rp."parcelId", rp."confirmedAt" DESC NULLS LAST
          )
        """;

    public const string CollectedJoin = """LEFT JOIN rp ON rp."parcelId" = p.id""";

    public const string DelayJoin = """LEFT JOIN ev ON ev."parcelId" = p.id""";

    /// <summary>`COD_TO_COLLECT` (`codToCollect` de `utils/money.ts`, en SQL).</summary>
    public const string CodToCollect = """
        (CASE
          WHEN p."feeIncluded" THEN (CASE WHEN p."alreadyPaid" THEN 0 ELSE p."codAmount" END)
          ELSE (CASE WHEN p."alreadyPaid" THEN p."deliveryFee" ELSE p."codAmount" + p."deliveryFee" END)
        END)
        """;

    public static readonly string Collected = $"""
        (CASE
          WHEN p."statusId" = {Delivered} AND p."financialStatus" <> 'NON_ENCAISSE'
          THEN COALESCE(rp."codCollected", {CodToCollect})
          ELSE 0
        END)
        """;

    public static readonly string CollectedCash = $"""
        (CASE
          WHEN p."statusId" = {Delivered} AND p."financialStatus" <> 'NON_ENCAISSE'
          THEN COALESCE(rp."codCash", rp."codCollected", {CodToCollect})
          ELSE 0
        END)
        """;

    public static readonly string CollectedCheque = $"""
        (CASE
          WHEN p."statusId" = {Delivered} AND p."financialStatus" <> 'NON_ENCAISSE'
          THEN COALESCE(rp."codCheque", 0)
          ELSE 0
        END)
        """;

    /// <summary>`sumCents` : la somme en centimes, arrondie en base.</summary>
    public static string SumCents(string expr) => $"COALESCE(ROUND(SUM({expr}) * 100), 0)::bigint";

    public static readonly string CountDelivered = $"""COUNT(*) FILTER (WHERE p."statusId" = {Delivered})::int""";

    public static readonly string CountReturned = $"""COUNT(*) FILTER (WHERE p."statusId" IN ({Ids(StatusCatalog.DashboardReturnStatusIds)}))::int""";

    public static readonly string CountFailed = $"""COUNT(*) FILTER (WHERE p."statusId" IN ({Ids(StatusCatalog.DashboardFailedStatusIds)}))::int""";

    /// <summary>`DELAY_CTE` : premier ramassage (3, 18) et première livraison (4), par événement.</summary>
    public static readonly string DelayCte = $"""
        , ev AS (
            SELECT e."parcelId",
              MIN(e."createdAt") FILTER (WHERE e."statusId" IN (3, 18)) AS picked_at,
              MIN(e."createdAt") FILTER (WHERE e."statusId" = {Delivered}) AS delivered_at
            FROM parcel_events e
            JOIN pop ON pop.id = e."parcelId" AND pop."statusId" = {Delivered}
            GROUP BY e."parcelId"
          )
        """;

    public const string DelayHours = """
        (CASE
          WHEN ev.picked_at IS NOT NULL AND ev.delivered_at IS NOT NULL AND ev.delivered_at >= ev.picked_at
          THEN EXTRACT(EPOCH FROM (ev.delivered_at - ev.picked_at)) / 3600.0
          ELSE NULL
        END)
        """;

    /// <summary>`bucketRows` : le seau (`width_bucket`, 1-indexé) et le statut de chaque colis.</summary>
    public static Task<List<DashboardBucketRow>> BucketRowsAsync(IAppDbContext db, ParcelPopulation pop, IReadOnlyList<(string Date, DateTime Start)> buckets, CancellationToken cancellationToken)
    {
        var sql = new DashboardSqlText();
        var starts = sql.P(buckets.Select(b => b.Start).ToArray());
        return sql.RunAsync<DashboardBucketRow>(db, $"""
            SELECT width_bucket(p."createdAt", {starts}::timestamptz[]) AS "Bucket",
                   p."statusId" AS "StatusId",
                   COUNT(*)::int AS "Count"
            FROM parcels p
            WHERE {Population(sql, pop)}
            GROUP BY 1, 2
            """, cancellationToken);
    }

    /// <summary>`Math.min(Math.max(bucket - 1, 0), length - 1)`.</summary>
    public static int BucketIndex(int bucket, int count) => Math.Min(Math.Max(bucket - 1, 0), count - 1);

    /// <summary>`pointsFor` : la courbe livrés / retours / en cours / échecs d'une fenêtre.</summary>
    public static async Task<List<DashboardSeriesPointDto>> PointsForAsync(IAppDbContext db, ParcelPopulation pop, PeriodWindow win, TimeZoneInfo zone, CancellationToken cancellationToken)
    {
        var buckets = Periods.Buckets(win, zone);
        var rows = await BucketRowsAsync(db, pop, buckets, cancellationToken);
        var points = buckets.Select(b => new DashboardSeriesPointDto { Date = b.Date }).ToList();
        foreach (var row in rows)
        {
            var point = points[BucketIndex(row.Bucket, buckets.Count)];
            switch (SeriesLine(row.StatusId))
            {
                case "delivered": point.Delivered += row.Count; break;
                case "returns": point.Returns += row.Count; break;
                case "failures": point.Failures += row.Count; break;
                default: point.InProgress += row.Count; break;
            }
        }
        return points;
    }

    /// <summary>`seriesLine` : le refus (6) compte en échec, pas en retour.</summary>
    public static string SeriesLine(int statusId)
    {
        if (statusId == Delivered) return "delivered";
        if (statusId != 6 && StatusCatalog.DashboardReturnStatusIds.Contains(statusId)) return "returns";
        if (StatusCatalog.DashboardFailedStatusIds.Contains(statusId)) return "failures";
        return "inProgress";
    }

    public static long Cents(long? value) => value ?? 0;
}
