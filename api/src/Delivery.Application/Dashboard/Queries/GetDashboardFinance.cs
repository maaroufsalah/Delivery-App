using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Dashboard;

/// <summary>
/// `GET /dashboard/finance` : COD encaissé / reversé / à remettre / en retard, règlements, OV, écarts et solde livreurs.
/// Le COD se lit sur la course qui a encaissé ; « à remettre » tient en deux lignes (chez les livreurs, rentré non reversé).
/// </summary>
public sealed record GetDashboardFinanceQuery(DashboardFilters Filters) : IRequest<DashboardFinanceDto>, IDashboardRequest;

public sealed class GetDashboardFinanceValidator : DashboardValidator<GetDashboardFinanceQuery>;

public sealed class DashboardCodRow
{
    public string FinancialStatus { get; set; } = string.Empty;
    public long Total { get; set; }
    public long Cash { get; set; }
    public long Cheque { get; set; }
}

internal sealed class GetDashboardFinanceHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetDashboardFinanceQuery, DashboardFinanceDto>
{
    public async Task<DashboardFinanceDto> Handle(GetDashboardFinanceQuery request, CancellationToken cancellationToken)
    {
        var ctx = await DashboardScope.ResolveAsync(db, caller, clock, request.Filters, cancellationToken);
        var thresholds = await DashboardThresholds.LoadAsync(db, ctx.Population.AgencyId, cancellationToken);
        var cod = await CodByFinancialStatusAsync(ctx.Population, cancellationToken);
        var late = await LateCodAsync(ctx.Population, thresholds, cancellationToken);
        var settlements = await SettlementsAsync(ctx, ctx.Win.Start, ctx.Win.End, cancellationToken);
        var transferOrders = await TransferOrdersAsync(ctx, ctx.Win.Start, ctx.Win.End, cancellationToken);
        var cashVariance = await CashVarianceAsync(ctx, ctx.Win.Start, ctx.Win.End, cancellationToken);
        var balance = await CourierBalanceAsync(ctx, cancellationToken);
        DashboardFinancePreviousDto? previous = null;
        if (ctx.Prev is { } prev && ctx.PreviousPopulation is { } previousPopulation)
        {
            var prevCod = await CodByFinancialStatusAsync(previousPopulation, cancellationToken);
            previous = new DashboardFinancePreviousDto(
                prevCod.Where(r => r.FinancialStatus != nameof(FinancialStatus.NON_ENCAISSE)).Sum(r => r.Total),
                prevCod.Where(r => r.FinancialStatus == nameof(FinancialStatus.REMBOURSE)).Sum(r => r.Total),
                (await SettlementsAsync(ctx, prev.Start, prev.End, cancellationToken)).Amount,
                (await TransferOrdersAsync(ctx, prev.Start, prev.End, cancellationToken)).Amount,
                (await CashVarianceAsync(ctx, prev.Start, prev.End, cancellationToken)).Amount);
        }

        DashboardMoneySplitDto Split(Func<string, bool> keep)
        {
            var rows = cod.Where(r => keep(r.FinancialStatus)).ToList();
            return new DashboardMoneySplitDto(rows.Sum(r => r.Total), rows.Sum(r => r.Cash), rows.Sum(r => r.Cheque));
        }

        return new DashboardFinanceDto(
            Split(s => s != nameof(FinancialStatus.NON_ENCAISSE)),
            Split(s => s == nameof(FinancialStatus.REMBOURSE)).Total,
            new DashboardToRemitDto(Split(s => s == nameof(FinancialStatus.ENCAISSE)), Split(s => s is nameof(FinancialStatus.REMIS_LIVREUR) or nameof(FinancialStatus.FACTURE))),
            new DashboardLateCodDto(late.Count, late.Amount, thresholds.CodLateHours),
            settlements,
            transferOrders,
            cashVariance,
            balance,
            previous);
    }

    /// <summary>Une requête : les cinq marches de l'argent, espèces et chèques séparés.</summary>
    private Task<List<DashboardCodRow>> CodByFinancialStatusAsync(ParcelPopulation pop, CancellationToken cancellationToken)
    {
        var sql = new DashboardSqlText();
        return sql.RunAsync<DashboardCodRow>(db, $"""
            WITH pop AS (
              SELECT {DashboardSql.PopColumns} FROM parcels p
              WHERE {DashboardSql.Population(sql, pop)}
            ){DashboardSql.CollectedCte}
            SELECT p."financialStatus"::text AS "FinancialStatus",
                   {DashboardSql.SumCents(DashboardSql.Collected)} AS "Total",
                   {DashboardSql.SumCents(DashboardSql.CollectedCash)} AS "Cash",
                   {DashboardSql.SumCents(DashboardSql.CollectedCheque)} AS "Cheque"
            FROM pop p {DashboardSql.CollectedJoin}
            GROUP BY 1
            """, cancellationToken);
    }

    /// <summary>COD en retard : encaissé, toujours chez le livreur, livré (événement 4) il y a plus de N heures.</summary>
    private async Task<DashboardAmountRow> LateCodAsync(ParcelPopulation pop, DashboardThresholdsDto thresholds, CancellationToken cancellationToken)
    {
        var sql = new DashboardSqlText();
        var before = clock.UtcNow.AddMilliseconds(-thresholds.CodLateHours * 3_600_000);
        var rows = await sql.RunAsync<DashboardAmountRow>(db, $"""
            WITH pop AS (
              SELECT {DashboardSql.PopColumns} FROM parcels p
              WHERE {DashboardSql.Population(sql, pop)}
                AND p."financialStatus" = 'ENCAISSE'
            ){DashboardSql.CollectedCte}{DashboardSql.DelayCte}
            SELECT COUNT(*)::int AS "Count", {DashboardSql.SumCents(DashboardSql.Collected)} AS "Amount"
            FROM pop p {DashboardSql.CollectedJoin} {DashboardSql.DelayJoin}
            WHERE ev.delivered_at IS NOT NULL AND ev.delivered_at < {sql.P(before)}
            """, cancellationToken);
        return rows.FirstOrDefault() ?? new DashboardAmountRow();
    }

    /// <summary>Règlements clients (`payments.paidAt`) : le filtre client s'y applique, pas le filtre livreur.</summary>
    private async Task<DashboardCountAmountDto> SettlementsAsync(DashboardContext ctx, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var payments = db.Payments.AsNoTracking().Where(p => p.PaidAt >= from && p.PaidAt < to);
        if (ctx.OwnAgency is { } agency) payments = payments.Where(p => p.AgencyId == agency);
        if (ctx.Population.ClientId is { } client) payments = payments.Where(p => p.ClientId == client);
        var count = await payments.CountAsync(cancellationToken);
        var sum = await payments.SumAsync(p => (decimal?)p.Amount, cancellationToken);
        return new DashboardCountAmountDto(count, Money.ToCents(sum));
    }

    /// <summary>Ordres de virement (OV-) créés dans la fenêtre.</summary>
    private async Task<DashboardCountAmountDto> TransferOrdersAsync(DashboardContext ctx, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var orders = db.TransferOrders.AsNoTracking().Where(o => o.CreatedAt >= from && o.CreatedAt < to);
        if (ctx.OwnAgency is { } agency) orders = orders.Where(o => o.AgencyId == agency);
        var count = await orders.CountAsync(cancellationToken);
        var sum = await orders.SumAsync(o => (decimal?)o.TotalDue, cancellationToken);
        return new DashboardCountAmountDto(count, Money.ToCents(sum));
    }

    /// <summary>Écarts de caisse : la `variance` rangée à la clôture des RF- confirmés dans la fenêtre (négatif : il manque de l'argent).</summary>
    private async Task<DashboardCountAmountDto> CashVarianceAsync(DashboardContext ctx, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var remittances = DashboardOps.Variances(db, ctx, from, to);
        var count = await remittances.CountAsync(cancellationToken);
        var sum = await remittances.SumAsync(r => (decimal?)r.Variance, cancellationToken);
        return new DashboardCountAmountDto(count, Money.ToCents(sum));
    }

    /// <summary>Le solde dû par les livreurs : un état, sans période.</summary>
    private async Task<long> CourierBalanceAsync(DashboardContext ctx, CancellationToken cancellationToken)
    {
        var ledger = db.CourierBalanceEntries.AsNoTracking();
        if (ctx.Population.CourierId is { } courier) ledger = ledger.Where(e => e.CourierId == courier);
        if (ctx.CourierAgency is { } agency) ledger = ledger.Where(e => e.Courier.AgencyId == agency);
        return Money.ToCents(await ledger.SumAsync(e => (decimal?)e.Amount, cancellationToken));
    }
}
