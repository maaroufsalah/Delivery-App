using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Finance;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.PettyCash;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Invoices;

/// <summary>`billableQuerySchema` : les colis facturables d'un vendeur, et ce que chacun pèserait sur sa facture.</summary>
public sealed record GetBillableParcelsQuery(string? ClientId, string? AgencyId, string? From, string? To, string? Search) : IRequest<IReadOnlyList<BillableParcelLineDto>>;

public sealed class GetBillableParcelsValidator : ZodValidator<GetBillableParcelsQuery>
{
    public GetBillableParcelsValidator()
    {
        RuleFor(x => x.ClientId).Required().MinLength(1, "required");
        RuleFor(x => x.Search).MaxLength(120);
    }
}

internal sealed class GetBillableParcelsHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetBillableParcelsQuery, IReadOnlyList<BillableParcelLineDto>>
{
    public async Task<IReadOnlyList<BillableParcelLineDto>> Handle(GetBillableParcelsQuery request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var where = ParcelFilters.Scoped(db.Parcels.AsNoTracking(), caller).Where(p => p.BillingState == BillingState.TO_INVOICE && !p.InvoiceLines.Any()
            && !(p.Client.BillingType == BillingType.NORMAL && p.CodPayouts.Any(l => l.PaidOutParcelId != null)));
        var clientId = caller.MayFilterBy(caller.ClientId) ? request.ClientId : caller.ClientId;
        if (clientId is not null) where = where.Where(p => p.ClientId == clientId);
        where = InvoiceRows.BillableNow(where, now);
        if (!string.IsNullOrEmpty(request.AgencyId) && caller.MayFilterBy(caller.AgencyId)) where = where.Where(p => p.AgencyId == request.AgencyId);
        var (from, to) = ReturnRows.DayRange(request.From, request.To);
        if (from is { } f) where = where.Where(p => p.StatusAt >= f);
        if (to is { } t) where = where.Where(p => p.StatusAt < t);
        if (!string.IsNullOrEmpty(request.Search))
        {
            var needle = request.Search.ToLower();
            where = where.Where(p => p.Code.ToLower().Contains(needle) || p.RecipientName.ToLower().Contains(needle));
        }
        var parcels = await where.OrderBy(p => p.StatusAt).Take(NoteRules.SelectionMax + 1)
            .Select(p => new
            {
                p.Id, p.Code, p.ClientId, Client = p.Client.Name, p.RecipientName, City = p.DestinationCity.Name, p.StatusId, p.StatusAt, p.CodAmount, p.DeliveryFee, p.FailureFee, p.FeeIncluded,
                p.AlreadyPaid, p.ReimbursedByCourier, CashReceived = p.Remittances.Any(l => l.ConfirmedParcelId != null),
            }).ToListAsync(cancellationToken);
        var vatRate = await InvoiceDrafts.VatRateAsync(db, cancellationToken);
        var client = await db.Clients.AsNoTracking().Where(c => c.Id == request.ClientId).Select(c => new { c.BillingType, c.IsPro, c.VatRate }).FirstOrDefaultAsync(cancellationToken);
        var billingType = client?.BillingType ?? BillingType.NORMAL;
        var proRate = client is null ? null : Billing.ProVatRateFor(client.IsPro, client.VatRate, client.IsPro ? await InvoiceDrafts.ProVatDefaultAsync(db, cancellationToken) : 0);
        var rate = proRate ?? vatRate;
        return parcels.Select(p =>
        {
            var billable = new BillableParcel(Money.ToCents(p.CodAmount), Money.ToCents(p.DeliveryFee), Money.ToCentsOrNull(p.FailureFee), 0, p.FeeIncluded, p.AlreadyPaid, p.StatusId);
            var fee = Billing.FeeCharged(billable);
            return new BillableParcelLineDto(p.Id, p.Code, p.ClientId, p.Client, p.RecipientName, p.City, p.StatusId, p.StatusAt, Money.ToCents(p.CodAmount), Money.ToCents(p.DeliveryFee),
                Money.ToCentsOrNull(p.FailureFee), Billing.VatOn(fee, rate), Billing.NetDueFor(InvoiceRows.WireBillingType(billingType), billable, Billing.DeliveredStatusId, rate), p.CashReceived,
                InvoiceRows.Billability(p.StatusId, p.ReimbursedByCourier, p.StatusAt, Money.ToCents(p.DeliveryFee), Money.ToCentsOrNull(p.FailureFee), now));
        }).ToList();
    }
}
