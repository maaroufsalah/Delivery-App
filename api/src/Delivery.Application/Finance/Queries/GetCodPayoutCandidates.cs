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

namespace Delivery.Application.Finance;

/// <summary>`codPayoutCandidateQuerySchema` : les colis versables d'un vendeur (cash remis, jamais versé, non facturé en régime normal).</summary>
public sealed record GetCodPayoutCandidatesQuery(string? ClientId, string? From, string? To, string? Search) : IRequest<IReadOnlyList<CodPayoutCandidateDto>>;

public sealed class GetCodPayoutCandidatesValidator : ZodValidator<GetCodPayoutCandidatesQuery>
{
    public GetCodPayoutCandidatesValidator()
    {
        RuleFor(x => x.ClientId).Required().MinLength(1, "required");
        RuleFor(x => x.Search).MaxLength(120);
    }
}

internal sealed class GetCodPayoutCandidatesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetCodPayoutCandidatesQuery, IReadOnlyList<CodPayoutCandidateDto>>
{
    public async Task<IReadOnlyList<CodPayoutCandidateDto>> Handle(GetCodPayoutCandidatesQuery request, CancellationToken cancellationToken)
    {
        var where = ParcelFilters.Scoped(db.Parcels.AsNoTracking(), caller).Where(p => p.StatusId == Billing.DeliveredStatusId && p.ReimbursedByCourier
            && !p.CodPayouts.Any(l => l.PaidOutParcelId != null) && !p.InvoiceLines.Any(l => l.InvoicedParcelId != null && l.Invoice.BillingType == BillingType.NORMAL));
        var clientId = caller.MayFilterBy(caller.ClientId) ? request.ClientId : caller.ClientId;
        if (clientId is not null) where = where.Where(p => p.ClientId == clientId);
        var (from, to) = ReturnRows.DayRange(request.From, request.To);
        if (from is { } f) where = where.Where(p => p.ReimbursedAt >= f);
        if (to is { } t) where = where.Where(p => p.ReimbursedAt < t);
        if (!string.IsNullOrEmpty(request.Search))
        {
            var needle = request.Search.ToLower();
            where = where.Where(p => p.Code.ToLower().Contains(needle) || p.RecipientName.ToLower().Contains(needle));
        }
        var rows = await where.OrderBy(p => p.ReimbursedAt).Take(NoteRules.SelectionMax + 1)
            .Select(p => new
            {
                p.Id, p.Code, p.RecipientName, City = p.DestinationCity.Name, p.CodAmount, p.DeliveryFee, p.FeeIncluded, p.AlreadyPaid, p.ReimbursedAt, p.StatusAt,
                ChequeAmount = p.Cheque == null ? (decimal?)null : p.Cheque.Amount, ChequeStatus = p.Cheque == null ? (ChequeStatus?)null : p.Cheque.Status,
                Remitted = p.Remittances.Where(r => r.Remittance.Status != CashRemittanceStatus.CANCELLED).Select(r => new ParcelCheques.Remitted(r.Cheque, r.ConfirmedParcelId)).ToList(),
            }).ToListAsync(cancellationToken);
        return rows.Select(p => new CodPayoutCandidateDto(p.Id, p.Code, p.RecipientName, p.City,
            ParcelCheques.Payable(p.CodAmount, p.DeliveryFee, p.FeeIncluded, p.AlreadyPaid, p.ChequeAmount, p.ChequeStatus, p.Remitted), p.ReimbursedAt, p.StatusAt)).Where(c => c.CodCollected > 0).ToList();
    }
}
