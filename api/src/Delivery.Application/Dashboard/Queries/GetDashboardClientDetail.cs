using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Parcels;
using Delivery.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Dashboard;

/// <summary>`GET /dashboard/clients/:id` : totaux, COD encaissé et reversé, délai, évolution et derniers colis d'un client sur la période.</summary>
public sealed record GetDashboardClientDetailQuery(string Id, DashboardFilters Filters) : IRequest<DashboardClientDetailDto>, IDashboardRequest;

public sealed class GetDashboardClientDetailValidator : DashboardValidator<GetDashboardClientDetailQuery>;

internal sealed class GetDashboardClientDetailHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IMapper mapper) : IRequestHandler<GetDashboardClientDetailQuery, DashboardClientDetailDto>
{
    public async Task<DashboardClientDetailDto> Handle(GetDashboardClientDetailQuery request, CancellationToken cancellationToken)
    {
        // Speedy lit le client sans contrôle de portée (seuls les chiffres sont bornés à la portée) : reproduit.
        var client = await db.Clients.AsNoTracking().Where(c => c.Id == request.Id).Select(c => new DashboardRankings.ClientLabel(c.Id, c.Name, c.IsPro))
                         .FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound("Client introuvable.");
        var ctx = await DashboardScope.ResolveAsync(db, caller, clock, request.Filters.WithClient(client.Id), cancellationToken);
        var pop = ctx.Population;
        var rows = await DashboardRankings.RankAsync(db, pop, DashboardRankings.ClientDimension, "COUNT(*) DESC", 1, 1, true, cancellationToken);
        var reversed = await ReversedAsync(pop, cancellationToken);
        var prevRows = ctx.PreviousPopulation is { } previousPopulation
            ? await DashboardRankings.RankAsync(db, previousPopulation, DashboardRankings.ClientDimension, "COUNT(*) DESC", 1, 1, false, cancellationToken)
            : null;
        var points = await DashboardSql.PointsForAsync(db, pop, ctx.Win, ctx.Zone, cancellationToken);
        var latestRows = await mapper.ProjectTo<ParcelRow>(DashboardScope.Parcels(db, pop).OrderByDescending(p => p.StatusAt).Take(10)).ToListAsync(cancellationToken);

        var row = rows.FirstOrDefault() ?? new DashboardRankRow { Id = client.Id };
        var prev = prevRows?.FirstOrDefault();
        var b = DashboardRankings.ClientRow(row, 1, client);
        return new DashboardClientDetailDto(
            client.Id,
            client.Name,
            client.IsPro,
            ctx.WindowDto(),
            new DashboardClientTotalsDto(b.ClientId, b.ClientName, b.IsPro, b.Total, b.Delivered, b.Returned, b.SuccessRate, DashboardScope.Ratio(row.Returned, row.Delivered + row.Returned),
                b.CodCollected, reversed, row.AvgDelay),
            ctx.PreviousPopulation is null ? null : new DashboardClientPreviousDto(prev?.Total ?? 0, prev?.Delivered ?? 0, prev?.Returned ?? 0),
            points,
            ctx.Win.Granularity,
            latestRows.Select(r => ParcelViews.ToListItem(r, caller)).ToList());
    }

    /// <summary>Le COD encaissé des colis passés en `REMBOURSE` (reversés au client).</summary>
    private async Task<long> ReversedAsync(ParcelPopulation pop, CancellationToken cancellationToken)
    {
        var sql = new DashboardSqlText();
        var rows = await sql.RunAsync<long>(db, $"""
            WITH pop AS (
              SELECT {DashboardSql.PopColumns} FROM parcels p
              WHERE {DashboardSql.Population(sql, pop)}
            ){DashboardSql.CollectedCte}
            SELECT {DashboardSql.SumCents(DashboardSql.Collected)} AS "Value"
            FROM pop p {DashboardSql.CollectedJoin}
            WHERE p."financialStatus" = 'REMBOURSE'
            """, cancellationToken);
        return rows.FirstOrDefault();
    }
}
