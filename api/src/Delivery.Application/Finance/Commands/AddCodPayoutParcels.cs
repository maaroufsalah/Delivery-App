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

/// <summary>`codPayoutParcelsSchema` : chaque colis livré, cash remis, du même vendeur, jamais versé, avec un montant versable.</summary>
public sealed record AddCodPayoutParcelsCommand : IRequest<CodPayoutDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public IReadOnlyList<string>? ParcelIds { get; init; }
}

public sealed class AddCodPayoutParcelsValidator : ZodValidator<AddCodPayoutParcelsCommand>
{
    public AddCodPayoutParcelsValidator()
    {
        RuleFor(x => x.ParcelIds).Required("array").Custom((ids, context) => RemittanceIds.Check(ids, context, required: true));
    }
}

internal sealed class AddCodPayoutParcelsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<AddCodPayoutParcelsCommand, CodPayoutDetailDto>
{
    public async Task<CodPayoutDetailDto> Handle(AddCodPayoutParcelsCommand input, CancellationToken cancellationToken)
    {
        var payout = await CodPayouts.Scoped(db.CodPayouts.AsNoTracking(), caller).Where(p => p.Code == input.Code).Select(p => new { p.Id, p.Status, p.ClientId }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound($"Bon de versement introuvable : {input.Code}");
        if (payout.Status != CodPayoutStatus.DRAFT) throw BusinessException.Conflict("Ce bon est versé : il ne se modifie plus.", new { reason = "payout-paid" });
        var ids = input.ParcelIds!;
        var parcels = await db.Parcels.AsNoTracking().Where(p => ids.Contains(p.Id) && p.DeletedAt == null)
            .Select(p => new
            {
                p.Id, p.Code, p.ClientId, p.StatusId, p.ReimbursedByCourier, p.CodAmount, p.DeliveryFee, p.FeeIncluded, p.AlreadyPaid,
                PaidOut = p.CodPayouts.Any(l => l.PaidOutParcelId != null),
                NormalInvoice = p.InvoiceLines.Where(l => l.InvoicedParcelId != null && l.Invoice.BillingType == BillingType.NORMAL).Select(l => l.Invoice.Code).FirstOrDefault(),
                ChequeAmount = p.Cheque == null ? (decimal?)null : p.Cheque.Amount, ChequeStatus = p.Cheque == null ? (ChequeStatus?)null : p.Cheque.Status,
                Remitted = p.Remittances.Where(r => r.Remittance.Status != CashRemittanceStatus.CANCELLED).Select(r => new ParcelCheques.Remitted(r.Cheque, r.ConfirmedParcelId)).ToList(),
            }).ToDictionaryAsync(p => p.Id, cancellationToken);
        var amounts = new Dictionary<string, long>();
        foreach (var parcelId in ids)
        {
            var parcel = parcels.GetValueOrDefault(parcelId) ?? throw BusinessException.NotFound($"Colis introuvable : {parcelId}", new { reason = "parcel-unknown", parcelId });
            if (parcel.ClientId != payout.ClientId) throw BusinessException.Conflict($"Le colis {parcel.Code} appartient à un autre e-commerçant.", new { reason = "parcel-other-client", code = parcel.Code });
            if (parcel.StatusId != Billing.DeliveredStatusId)
            {
                throw BusinessException.Conflict($"Le colis {parcel.Code} n'est pas livré.", new { reason = "parcel-not-delivered", code = parcel.Code, statusId = parcel.StatusId });
            }
            if (!parcel.ReimbursedByCourier)
            {
                throw BusinessException.Conflict($"Le cash du colis {parcel.Code} n'a pas encore été remis par le livreur.", new { reason = "parcel-not-reimbursed", code = parcel.Code });
            }
            if (parcel.PaidOut) throw BusinessException.Conflict($"Le COD du colis {parcel.Code} a déjà été versé.", new { reason = "parcel-already-paid-out", code = parcel.Code });
            if (parcel.NormalInvoice is { } invoice) throw CodPayouts.AlreadyInvoiced(parcel.Code, invoice);
            var amount = ParcelCheques.Payable(parcel.CodAmount, parcel.DeliveryFee, parcel.FeeIncluded, parcel.AlreadyPaid, parcel.ChequeAmount, parcel.ChequeStatus, parcel.Remitted);
            if (amount == 0)
            {
                throw BusinessException.Conflict($"Le colis {parcel.Code} est payé par chèque : rien à verser, le chèque est transmis au vendeur.", new { reason = "parcel-paid-by-cheque", code = parcel.Code });
            }
            amounts[parcelId] = amount;
        }
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var (parcelId, amount) in amounts)
        {
            var line = await db.CodPayoutParcels.FirstOrDefaultAsync(l => l.PayoutId == payout.Id && l.ParcelId == parcelId, cancellationToken);
            if (line is null) db.CodPayoutParcels.Add(new CodPayoutParcel { PayoutId = payout.Id, ParcelId = parcelId, Amount = Money.FromCents(amount) });
            else line.Amount = Money.FromCents(amount);
        }
        await db.SaveChangesAsync(cancellationToken);
        await CodPayouts.RefreshTotalAsync(db, payout.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await CodPayouts.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}
