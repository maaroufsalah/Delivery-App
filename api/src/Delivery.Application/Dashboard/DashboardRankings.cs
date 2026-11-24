using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Dashboard;

/// <summary>`DashboardRankingsService` : classements paginés triés en base (livreurs, villes, clients).</summary>
public static class DashboardRankings
{
    public const string CourierDimension = """p."courierId" """;
    public const string CityDimension = """p."destinationCityId" """;
    public const string ClientDimension = """p."clientId" """;

    public static async Task<Paginated<DashboardCourierRowDto>> CouriersAsync(IAppDbContext db, DashboardContext ctx, int page, int pageSize, string sort, CancellationToken cancellationToken)
    {
        var d = DashboardSql.CountDelivered;
        var r = DashboardSql.CountReturned;
        var order = sort switch
        {
            "successRate" => $"(CASE WHEN {d} + {r} = 0 THEN NULL ELSE {d}::float8 / ({d} + {r}) END) DESC NULLS LAST, {d} DESC",
            "codCollected" => $"{DashboardSql.SumCents(DashboardSql.Collected)} DESC, {d} DESC",
            "assigned" => $"COUNT(*) DESC, {d} DESC",
            _ => $"{d} DESC, COUNT(*) DESC",
        };
        var rows = await RankAsync(db, ctx.Population, CourierDimension, order, page, pageSize, true, cancellationToken);
        var total = await DistinctAsync(db, ctx.Population, CourierDimension, cancellationToken);
        var ids = rows.Select(x => x.Id).ToList();
        var names = await CourierNamesAsync(db, ids, cancellationToken);
        var previous = await PreviousAsync(db, ctx.PreviousPopulation, CourierDimension, DashboardSql.CountDelivered, ids, cancellationToken);
        var offset = (page - 1) * pageSize;
        var items = rows.Select((row, index) => new DashboardCourierRowDto(offset + index + 1, row.Id, names.GetValueOrDefault(row.Id, "-"), row.Total, row.Delivered, row.Returned,
            DashboardScope.Ratio(row.Delivered, row.Delivered + row.Returned), row.AvgDelay, row.Collected, previous is null ? null : previous.GetValueOrDefault(row.Id))).ToList();
        return new Paginated<DashboardCourierRowDto>(items, total, page, pageSize);
    }

    public static async Task<Paginated<DashboardCityRowDto>> CitiesAsync(IAppDbContext db, DashboardContext ctx, int page, int pageSize, string sort, CancellationToken cancellationToken)
    {
        var d = DashboardSql.CountDelivered;
        var closed = $"({d} + {DashboardSql.CountReturned})";
        var order = sort switch
        {
            "successRate" => $"(CASE WHEN {closed} = 0 THEN NULL ELSE {d}::float8 / {closed} END) DESC NULLS LAST, COUNT(*) DESC",
            "returnRate" => $"(CASE WHEN {closed} = 0 THEN NULL ELSE {DashboardSql.CountReturned}::float8 / {closed} END) DESC NULLS LAST, COUNT(*) DESC",
            "codCollected" => $"{DashboardSql.SumCents(DashboardSql.Collected)} DESC, COUNT(*) DESC",
            _ => $"COUNT(*) DESC, {d} DESC",
        };
        var rows = await RankAsync(db, ctx.Population, CityDimension, order, page, pageSize, false, cancellationToken);
        var total = await DistinctAsync(db, ctx.Population, CityDimension, cancellationToken);
        var ids = rows.Select(x => x.Id).ToList();
        var names = await db.Cities.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        var items = rows.Select(row => new DashboardCityRowDto(row.Id, names.GetValueOrDefault(row.Id, row.Id), row.Total, row.Delivered, row.Returned,
            DashboardScope.Ratio(row.Delivered, row.Delivered + row.Returned), DashboardScope.Ratio(row.Returned, row.Delivered + row.Returned), row.Collected)).ToList();
        return new Paginated<DashboardCityRowDto>(items, total, page, pageSize);
    }

    public static async Task<DashboardClientsDto> ClientsAsync(IAppDbContext db, DashboardContext ctx, int page, int pageSize, string rank, CancellationToken cancellationToken)
    {
        var d = DashboardSql.CountDelivered;
        var closed = $"({d} + {DashboardSql.CountReturned})";
        var byVolume = $"COUNT(*) DESC, {d} DESC";
        var order = rank switch
        {
            "cod" => $"{DashboardSql.SumCents(DashboardSql.Collected)} DESC, COUNT(*) DESC",
            // Un client sans colis clos n'a pas de taux : il passe derrière.
            "successRate" => $"(CASE WHEN {closed} = 0 THEN NULL ELSE {d}::float8 / {closed} END) DESC NULLS LAST, COUNT(*) DESC",
            _ => byVolume,
        };
        var rows = await RankAsync(db, ctx.Population, ClientDimension, order, page, pageSize, false, cancellationToken);
        var total = await DistinctAsync(db, ctx.Population, ClientDimension, cancellationToken);
        var mostActive = (await RankAsync(db, ctx.Population, ClientDimension, byVolume, 1, 1, false, cancellationToken)).FirstOrDefault();
        var ids = rows.Select(x => x.Id).ToList();
        if (mostActive is not null && !ids.Contains(mostActive.Id)) ids.Add(mostActive.Id);
        var clients = await db.Clients.AsNoTracking().Where(c => ids.Contains(c.Id)).Select(c => new ClientLabel(c.Id, c.Name, c.IsPro)).ToDictionaryAsync(c => c.Id, cancellationToken);
        var previous = mostActive is null ? null : await PreviousAsync(db, ctx.PreviousPopulation, ClientDimension, "COUNT(*)::int", [mostActive.Id], cancellationToken);
        var offset = (page - 1) * pageSize;
        return new DashboardClientsDto(rank, rows.Select((row, index) => ClientRow(row, offset + index + 1, clients.GetValueOrDefault(row.Id))).ToList(), total, page, pageSize,
            mostActive is null
                ? null
                : new DashboardMostActiveClientDto(mostActive.Id, clients.GetValueOrDefault(mostActive.Id)?.Name ?? "-", mostActive.Total,
                    previous is null ? null : previous.GetValueOrDefault(mostActive.Id)));
    }

    public sealed record ClientLabel(string Id, string Name, bool IsPro);

    public static DashboardClientRowDto ClientRow(DashboardRankRow row, int rank, ClientLabel? client) =>
        new(rank, row.Id, client?.Name ?? "-", client?.IsPro ?? false, row.Total, row.Delivered, row.Returned, DashboardScope.Ratio(row.Delivered, row.Delivered + row.Returned),
            row.Collected);

    /// <summary>`rank` : une ligne par valeur de la dimension, avec le COD encaissé et (facultatif) le délai moyen.</summary>
    public static Task<List<DashboardRankRow>> RankAsync(IAppDbContext db, ParcelPopulation pop, string dimension, string order, int page, int pageSize, bool delays,
        CancellationToken cancellationToken)
    {
        var sql = new DashboardSqlText();
        var avgDelay = delays ? $"AVG({DashboardSql.DelayHours})::float8" : "NULL::float8";
        return sql.RunAsync<DashboardRankRow>(db, $"""
            WITH pop AS (
              SELECT {DashboardSql.PopColumns} FROM parcels p
              WHERE {DashboardSql.Population(sql, pop)} AND {dimension} IS NOT NULL
            ){DashboardSql.CollectedCte}{(delays ? DashboardSql.DelayCte : string.Empty)}
            SELECT {dimension} AS "Id",
                   COUNT(*)::int AS "Total",
                   {DashboardSql.CountDelivered} AS "Delivered",
                   {DashboardSql.CountReturned} AS "Returned",
                   {DashboardSql.SumCents(DashboardSql.Collected)} AS "Collected",
                   {avgDelay} AS "AvgDelay"
            FROM pop p {DashboardSql.CollectedJoin} {(delays ? DashboardSql.DelayJoin : string.Empty)}
            GROUP BY 1
            ORDER BY {order}, 1
            LIMIT {pageSize} OFFSET {(page - 1) * pageSize}
            """, cancellationToken);
    }

    /// <summary>`distinct` : le nombre de valeurs de la dimension (le total paginé).</summary>
    private static async Task<int> DistinctAsync(IAppDbContext db, ParcelPopulation pop, string dimension, CancellationToken cancellationToken)
    {
        var sql = new DashboardSqlText();
        var rows = await sql.RunAsync<int>(db, $"""SELECT COUNT(DISTINCT {dimension})::int AS "Value" FROM parcels p WHERE {DashboardSql.Population(sql, pop)}""", cancellationToken);
        return rows.FirstOrDefault();
    }

    /// <summary>`previousDelivered` / `previousTotal` : <c>null</c> sans période précédente, vide sans identifiant.</summary>
    private static async Task<Dictionary<string, int>?> PreviousAsync(IAppDbContext db, ParcelPopulation? pop, string dimension, string measure, IReadOnlyList<string> ids,
        CancellationToken cancellationToken)
    {
        if (pop is null) return null;
        if (ids.Count == 0) return [];
        var sql = new DashboardSqlText();
        var rows = await sql.RunAsync<DashboardCountRow>(db, $"""
            SELECT {dimension} AS "Id", {measure} AS "Value"
            FROM parcels p
            WHERE {DashboardSql.Population(sql, pop)}
              AND {dimension} = ANY({sql.P(ids.ToArray())}::text[])
            GROUP BY 1
            """, cancellationToken);
        return rows.ToDictionary(x => x.Id, x => x.Value);
    }

    /// <summary>`courierNames` : « prénom nom ».</summary>
    public static async Task<Dictionary<string, string>> CourierNamesAsync(IAppDbContext db, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return [];
        var rows = await db.Couriers.AsNoTracking().Where(c => ids.Contains(c.Id)).Select(c => new { c.Id, c.FirstName, c.LastName }).ToListAsync(cancellationToken);
        return rows.ToDictionary(c => c.Id, c => $"{c.FirstName} {c.LastName}".Trim());
    }
}
