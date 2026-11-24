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

/// <summary>`dashboardQuerySchema` : cash à remonter, remis non facturé, impayés, frais du mois.</summary>
public sealed record GetFinanceDashboardQuery(string? AgencyId) : IRequest<FinanceDashboardDto>;

internal sealed class GetFinanceDashboardHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetFinanceDashboardQuery, FinanceDashboardDto>
{
    private const int OverdueDays = 30;

    public async Task<FinanceDashboardDto> Handle(GetFinanceDashboardQuery request, CancellationToken cancellationToken)
    {
        var parcels = ParcelFilters.Scoped(db.Parcels.AsNoTracking(), caller);
        if (!string.IsNullOrEmpty(request.AgencyId) && caller.IsSuperAdmin()) parcels = parcels.Where(p => p.AgencyId == request.AgencyId);
        var now = clock.UtcNow;
        var monthStart = Periods.ZonedDayStart(Periods.FirstOfMonth(clock.Today()), ClockDays.Zone);
        var toRemit = await parcels.Where(p => p.StatusId == Billing.DeliveredStatusId && p.CourierId != null && p.FinancialStatus != FinancialStatus.NON_ENCAISSE
                                               && !p.Remittances.Any(l => l.ConfirmedParcelId != null))
            .Select(p => new { p.CourierId, Courier = p.Courier != null ? p.Courier.FirstName + " " + p.Courier.LastName : null, p.CodAmount, p.DeliveryFee, p.FeeIncluded, p.AlreadyPaid })
            .ToListAsync(cancellationToken);
        var remitted = await parcels.Where(p => p.StatusId == Billing.DeliveredStatusId && p.BillingState == BillingState.TO_INVOICE && p.Remittances.Any(l => l.ConfirmedParcelId != null))
            .Select(p => new { p.CodAmount, p.DeliveryFee, p.FeeIncluded, p.AlreadyPaid }).ToListAsync(cancellationToken);
        var invoices = Invoices(request.AgencyId);
        var unpaid = await invoices.Where(i => i.Status == InvoiceStatus.ISSUED).OrderBy(i => i.IssuedAt)
            .Select(i => new { i.TotalDue, i.IssuedAt, Paid = i.Payments.Select(p => p.Amount).ToList() }).ToListAsync(cancellationToken);
        var fees = await db.InvoiceLines.AsNoTracking().Where(l => invoices.Any(i => i.Id == l.InvoiceId && i.IssuedAt >= monthStart))
            .Select(l => new { l.DeliveryFee, l.FailureFee }).ToListAsync(cancellationToken);

        var byCourier = toRemit.GroupBy(p => p.CourierId!).Select(g => new CashToRemitDto(g.Key, g.First().Courier ?? "-", g.Count(),
            g.Sum(p => Remittances.Collected(p.CodAmount, p.DeliveryFee, p.FeeIncluded, p.AlreadyPaid)))).OrderByDescending(r => r.Amount).ToList();
        long amount = 0;
        var overdue = 0;
        DateTime? oldest = null;
        foreach (var invoice in unpaid)
        {
            var due = Money.ToCents(invoice.TotalDue);
            var paid = invoice.Paid.Sum(p => Money.ToCents(p));
            amount += Math.Abs(due) - Math.Min(paid, Math.Abs(due));
            if (invoice.IssuedAt is { } issued)
            {
                if (oldest is null || issued < oldest) oldest = issued;
                if (issued < now.AddDays(-OverdueDays)) overdue++;
            }
        }
        return new FinanceDashboardDto(byCourier, toRemit.Sum(p => Remittances.Collected(p.CodAmount, p.DeliveryFee, p.FeeIncluded, p.AlreadyPaid)),
            new CountAmountDto(remitted.Count, remitted.Sum(p => Remittances.Collected(p.CodAmount, p.DeliveryFee, p.FeeIncluded, p.AlreadyPaid))),
            new UnpaidInvoicesDto(unpaid.Count, amount, overdue, oldest, oldest is { } o ? (int)Math.Floor((now - o).TotalDays) : null),
            fees.Sum(l => Money.ToCents(l.DeliveryFee) + Money.ToCents(l.FailureFee)));
    }

    /// <summary>`invoiceWhere` (le vendeur n'entre pas : garde `NotMerchant`).</summary>
    private IQueryable<Domain.Entities.Invoice> Invoices(string? agencyId)
    {
        var invoices = db.Invoices.AsNoTracking();
        if (caller.IsSuperAdmin()) return string.IsNullOrEmpty(agencyId) ? invoices : invoices.Where(i => i.AgencyId == agencyId);
        if (caller.ClientId is { } clientId) return invoices.Where(i => i.ClientId == clientId && i.Status != InvoiceStatus.DRAFT);
        if (caller.CourierId is not null) return invoices.Where(i => i.Id == Scope.DenyAllId);
        if (caller.AgencyId is { } own) return invoices.Where(i => i.AgencyId == own);
        return invoices.Where(i => i.Id == Scope.DenyAllId);
    }
}
