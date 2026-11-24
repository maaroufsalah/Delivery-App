using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Parcels;
using MediatR;

namespace Delivery.Application.Dashboard;

/// <summary>`GET /dashboard/routes` : circuits de livraison ouverts, clôturés, en retard (24 h / 48 h) et moyennes par circuit.</summary>
public sealed record GetDashboardRoutesQuery(DashboardFilters Filters) : IRequest<DashboardRoutesDto>, IDashboardRequest;

public sealed class GetDashboardRoutesValidator : DashboardValidator<GetDashboardRoutesQuery>;

public sealed class DashboardRouteAggRow
{
    public int Closed { get; set; }
    public double? AvgParcels { get; set; }
    public double? AvgDelivered { get; set; }
    public double? AvgReturned { get; set; }
    public long? AvgCod { get; set; }
    public double? AvgDuration { get; set; }
}

internal sealed class GetDashboardRoutesHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetDashboardRoutesQuery, DashboardRoutesDto>
{
    public async Task<DashboardRoutesDto> Handle(GetDashboardRoutesQuery request, CancellationToken cancellationToken)
    {
        var ctx = await DashboardScope.ResolveAsync(db, caller, clock, request.Filters, cancellationToken);
        var routes = DashboardOps.Routes(db, ctx);
        var now = clock.UtcNow;
        var open = await DashboardOps.OpenCountAsync(routes, null, cancellationToken);
        var late24 = await DashboardOps.OpenCountAsync(routes, now.AddHours(-24), cancellationToken);
        var late48 = await DashboardOps.OpenCountAsync(routes, now.AddHours(-48), cancellationToken);
        var agg = await AggAsync(ctx, ctx.Win.Start, ctx.Win.End, cancellationToken);
        var prev = ctx.Prev is { } previous ? await AggAsync(ctx, previous.Start, previous.End, cancellationToken) : null;
        return new DashboardRoutesDto(open, agg.Closed, late24, late48, agg.AvgParcels, agg.AvgDelivered, agg.AvgReturned, agg.AvgCod, agg.AvgDuration,
            prev is null ? null : new DashboardRoutesPreviousDto(prev.Closed, prev.AvgDuration));
    }

    /// <summary>`routeAgg` : les circuits créés dans la fenêtre, leurs colis, livrés, retours, COD et durée (clôturés).</summary>
    private async Task<DashboardRouteAggRow> AggAsync(DashboardContext ctx, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var sql = new DashboardSqlText();
        var parts = new List<string> { "r.kind = 'DELIVERY'", $"""r."createdAt" >= {sql.P(from)}""", $"""r."createdAt" < {sql.P(to)}""" };
        if (ctx.Population.CourierId is { } courier) parts.Add($"""r."courierId" = {sql.P(courier)}""");
        if (ctx.Population.DestinationCityId is { } city) parts.Add($"""r."cityId" = {sql.P(city)}""");
        if (ctx.CourierAgency is { } agency) parts.Add($"""r."courierId" IN (SELECT c.id FROM couriers c WHERE c."agencyId" = {sql.P(agency)})""");
        if (ctx.Population.DenyAll) parts.Add("FALSE");
        var d = DashboardSql.Delivered;
        var rows = await sql.RunAsync<DashboardRouteAggRow>(db, $"""
            WITH r AS (
              SELECT r.id, r.status, r."createdAt", r."statusAt",
                     (SELECT COUNT(*)::int FROM route_parcels rp WHERE rp."routeId" = r.id) AS parcels,
                     (SELECT COUNT(*)::int FROM route_parcels rp WHERE rp."routeId" = r.id AND rp."finalStatusId" = {d}) AS delivered,
                     (SELECT COUNT(*)::int FROM route_parcels rp WHERE rp."routeId" = r.id AND rp."finalStatusId" IN ({DashboardSql.Ids(StatusCatalog.DashboardReturnStatusIds)})) AS returned,
                     (SELECT COALESCE(SUM(rp."codCollected"), 0) FROM route_parcels rp WHERE rp."routeId" = r.id AND rp."finalStatusId" = {d}) AS cod
              FROM routes r
              WHERE {string.Join(" AND ", parts)}
            )
            SELECT COUNT(*) FILTER (WHERE r.status = 'CLOSED')::int AS "Closed",
                   AVG(r.parcels)::float8 AS "AvgParcels",
                   AVG(r.delivered)::float8 AS "AvgDelivered",
                   AVG(r.returned)::float8 AS "AvgReturned",
                   ROUND(AVG(r.cod) * 100)::bigint AS "AvgCod",
                   AVG(EXTRACT(EPOCH FROM (r."statusAt" - r."createdAt")) / 3600.0) FILTER (WHERE r.status = 'CLOSED')::float8 AS "AvgDuration"
            FROM r
            """, cancellationToken);
        return rows.FirstOrDefault() ?? new DashboardRouteAggRow();
    }
}
