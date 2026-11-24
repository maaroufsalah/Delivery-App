using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.Parcels;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Finance;

/// <summary>`financeOverviewQuerySchema` : sept blocs agrégés sur la période, sous la portée du compte.</summary>
public sealed record GetFinanceOverviewQuery(string? Period, string? From, string? To, string? CourierId, string? AgencyId) : IRequest<FinanceOverviewDto>;

public sealed class GetFinanceOverviewValidator : ZodValidator<GetFinanceOverviewQuery>
{
    public GetFinanceOverviewValidator()
    {
        RuleFor(x => x.Period).OneOf(Periods.All);
    }
}

internal sealed class GetFinanceOverviewHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetFinanceOverviewQuery, FinanceOverviewDto>
{
    public async Task<FinanceOverviewDto> Handle(GetFinanceOverviewQuery request, CancellationToken cancellationToken)
    {
        var courierId = caller.CourierId ?? request.CourierId;
        var agencyId = caller.IsSuperAdmin() ? request.AgencyId : null;
        var timeZone = await db.Companies.AsNoTracking().OrderBy(c => c.CreatedAt).Select(c => c.Timezone).FirstOrDefaultAsync(cancellationToken);
        var allParcels = Parcels(courierId, agencyId);
        var first = await allParcels.OrderBy(p => p.CreatedAt).Select(p => (DateTime?)p.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        var window = Periods.Resolve(request.Period ?? "month", request.From, request.To, Periods.Zone(string.IsNullOrEmpty(timeZone) ? "Africa/Casablanca" : timeZone), first, clock.UtcNow);
        var (from, to) = (window.Start, window.End);

        var remittances = db.CashRemittances.AsNoTracking();
        if (courierId is not null) remittances = remittances.Where(r => r.CourierId == courierId);
        if (agencyId is not null) remittances = remittances.Where(r => r.AgencyId == agencyId);
        remittances = Remittances.Scoped(remittances, caller);
        var cashRows = await remittances.Where(r => r.CreatedAt >= from && r.CreatedAt < to)
            .GroupBy(r => r.Status).Select(g => new
            {
                Status = g.Key, Count = g.Count(), Cod = g.Sum(r => r.ExpectedCod), Cash = g.Sum(r => r.TotalCash), Cheque = g.Sum(r => r.TotalCheque), Commission = g.Sum(r => r.ExpectedCommission),
                Net = g.Sum(r => r.ExpectedNet), Declared = g.Sum(r => r.DeclaredAmount), Variance = g.Sum(r => r.Variance),
            }).ToListAsync(cancellationToken);
        var cash = new OverviewCashDto(cashRows.Sum(r => Money.ToCents(r.Cod)), cashRows.Sum(r => Money.ToCents(r.Cash)), cashRows.Sum(r => Money.ToCents(r.Cheque)),
            cashRows.Sum(r => Money.ToCents(r.Commission)), cashRows.Sum(r => Money.ToCents(r.Net)), cashRows.Sum(r => Money.ToCentsOrNull(r.Declared) ?? 0), cashRows.Sum(r => Money.ToCents(r.Variance)),
            cashRows.FirstOrDefault(r => r.Status == CashRemittanceStatus.DRAFT)?.Count ?? 0, cashRows.FirstOrDefault(r => r.Status == CashRemittanceStatus.CONFIRMED)?.Count ?? 0);

        var ledger = db.CourierBalanceEntries.AsNoTracking();
        if (courierId is not null) ledger = ledger.Where(e => e.CourierId == courierId);
        if (agencyId is not null) ledger = ledger.Where(e => e.Courier.AgencyId == agencyId);
        if (!caller.IsSuperAdmin())
        {
            if (caller.CourierId is { } own) ledger = ledger.Where(e => e.CourierId == own);
            else if (caller.ClientId is not null) ledger = ledger.Where(e => e.Id == Scope.DenyAllId);
            else if (caller.AgencyId is { } agency) ledger = ledger.Where(e => e.Courier.AgencyId == agency);
            else ledger = ledger.Where(e => e.Id == Scope.DenyAllId);
        }
        var sums = await ledger.GroupBy(e => e.CourierId).Select(g => new { CourierId = g.Key, Sum = g.Sum(e => e.Amount) }).ToListAsync(cancellationToken);
        var ids = sums.Select(s => s.CourierId).ToList();
        var names = await db.Couriers.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => $"{c.FirstName} {c.LastName}", cancellationToken);
        var balanceRows = sums.Select(s => new OverviewBalanceRowDto(s.CourierId, names.GetValueOrDefault(s.CourierId, "-"), Money.ToCents(s.Sum))).Where(r => r.Balance != 0)
            .OrderBy(r => r.Balance).ToList();

        var cheques = db.Cheques.AsNoTracking();
        if (courierId is not null) cheques = cheques.Where(c => c.CourierId == courierId);
        if (agencyId is not null) cheques = cheques.Where(c => c.Parcel.AgencyId == agencyId);
        cheques = Cheques.SlipScoped(cheques, caller);
        var chequeRows = await cheques.Where(c => c.ReceivedAt >= from && c.ReceivedAt < to).GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Sum = g.Sum(c => c.Amount) }).ToListAsync(cancellationToken);
        CountSumDto Bucket(Func<ChequeStatus, bool> keep) =>
            chequeRows.Where(r => keep(r.Status)).Aggregate(new CountSumDto(0, 0), (acc, r) => new CountSumDto(acc.Count + r.Count, acc.Amount + Money.ToCents(r.Sum)));
        var chequeBlock = new OverviewChequesDto(Bucket(_ => true), Bucket(s => s is not (ChequeStatus.ENCAISSE or ChequeStatus.REJETE or ChequeStatus.ANNULE)),
            Bucket(s => s == ChequeStatus.ENCAISSE), Bucket(s => s == ChequeStatus.REJETE));

        var inPeriod = allParcels.Where(p => p.CreatedAt >= from && p.CreatedAt < to);
        var financial = (await inPeriod.GroupBy(p => p.FinancialStatus).Select(g => new { Status = g.Key, Count = g.Count(), Sum = g.Sum(p => p.CodAmount) }).ToListAsync(cancellationToken))
            .Select(r => new OverviewFinancialStatusDto(Money.ToWire(r.Status), r.Count, Money.ToCents(r.Sum))).OrderByDescending(r => r.Count).ToList();
        var delivered = await inPeriod.CountAsync(p => p.StatusId == Billing.DeliveredStatusId, cancellationToken);

        var invoices = db.Invoices.AsNoTracking();
        if (agencyId is not null) invoices = invoices.Where(i => i.AgencyId == agencyId);
        if (!caller.IsSuperAdmin())
        {
            if (caller.ClientId is { } client) invoices = invoices.Where(i => i.ClientId == client && i.Status != InvoiceStatus.DRAFT);
            else if (caller.CourierId is not null) invoices = invoices.Where(i => i.Id == Scope.DenyAllId);
            else if (caller.AgencyId is { } agency) invoices = invoices.Where(i => i.AgencyId == agency);
            else invoices = invoices.Where(i => i.Id == Scope.DenyAllId);
        }
        var invoiceRows = await invoices.Where(i => i.CreatedAt >= from && i.CreatedAt < to).GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Sum = g.Sum(i => i.TotalDue) }).ToListAsync(cancellationToken);
        CountSumDto InvoiceBucket(InvoiceStatus status) => invoiceRows.FirstOrDefault(r => r.Status == status) is { } row ? new CountSumDto(row.Count, Math.Abs(Money.ToCents(row.Sum))) : new CountSumDto(0, 0);

        var statements = db.CourierStatements.AsNoTracking();
        if (courierId is not null) statements = statements.Where(s => s.CourierId == courierId);
        if (agencyId is not null) statements = statements.Where(s => s.AgencyId == agencyId);
        if (!caller.IsSuperAdmin())
        {
            if (caller.CourierId is { } own) statements = statements.Where(s => s.CourierId == own);
            else if (caller.ClientId is not null) statements = statements.Where(s => s.Id == Scope.DenyAllId);
            else if (caller.AgencyId is { } agency) statements = statements.Where(s => s.AgencyId == agency);
            else statements = statements.Where(s => s.Id == Scope.DenyAllId);
        }
        var statementRows = await statements.Where(s => s.CreatedAt >= from && s.CreatedAt < to).GroupBy(s => s.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Sum = g.Sum(s => s.TotalDue) }).ToListAsync(cancellationToken);
        CountSumDto StatementBucket(Func<CourierStatementStatus, bool> keep) =>
            statementRows.Where(r => keep(r.Status)).Aggregate(new CountSumDto(0, 0), (acc, r) => new CountSumDto(acc.Count + r.Count, acc.Amount + Money.ToCents(r.Sum)));

        return new FinanceOverviewDto(window.Period, window.From, window.To, courierId, cash, new OverviewBalancesDto(balanceRows.Sum(r => r.Balance), balanceRows), chequeBlock, financial,
            delivered, new OverviewInvoicesDto(InvoiceBucket(InvoiceStatus.ISSUED), InvoiceBucket(InvoiceStatus.PAID)),
            new OverviewStatementsDto(StatementBucket(s => s == CourierStatementStatus.DRAFT), StatementBucket(s => s is CourierStatementStatus.VALIDATED or CourierStatementStatus.PAID)));
    }

    /// <summary>`parcelWhere` sans borne : la portée, le livreur et l'agence demandés.</summary>
    private IQueryable<Domain.Entities.Parcel> Parcels(string? courierId, string? agencyId)
    {
        var parcels = db.Parcels.AsNoTracking().Where(p => p.DeletedAt == null);
        if (courierId is not null) parcels = parcels.Where(p => p.CourierId == courierId);
        if (agencyId is not null) parcels = parcels.Where(p => p.AgencyId == agencyId);
        if (caller.IsSuperAdmin()) return parcels;
        if (caller.CourierId is { } own) return parcels.Where(p => p.CourierId == own);
        if (caller.ClientId is { } client) return parcels.Where(p => p.ClientId == client);
        if (caller.AgencyId is { } agency) return parcels.Where(p => p.AgencyId == agency);
        return parcels.Where(p => p.Id == Scope.DenyAllId);
    }
}
