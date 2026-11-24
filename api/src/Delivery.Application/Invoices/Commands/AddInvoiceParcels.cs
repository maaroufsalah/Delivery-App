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

/// <summary>`invoiceParcelsSchema` : des colis facturables du même vendeur, sur aucune autre facture.</summary>
public sealed record AddInvoiceParcelsCommand : IRequest<InvoiceDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public IReadOnlyList<string>? ParcelIds { get; init; }
}

public sealed class AddInvoiceParcelsValidator : ZodValidator<AddInvoiceParcelsCommand>
{
    public AddInvoiceParcelsValidator()
    {
        RuleFor(x => x.ParcelIds).Required("array").Custom((ids, context) => RemittanceIds.Check(ids, context, required: true));
    }
}

internal sealed class AddInvoiceParcelsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<AddInvoiceParcelsCommand, InvoiceDetailDto>
{
    public async Task<InvoiceDetailDto> Handle(AddInvoiceParcelsCommand input, CancellationToken cancellationToken)
    {
        var invoice = await InvoiceRows.RequireDraftAsync(db, caller, input.Code, cancellationToken);
        var ids = input.ParcelIds!;
        var checks = await db.Parcels.AsNoTracking().Where(p => ids.Contains(p.Id) && p.DeletedAt == null)
            .Select(p => new
            {
                p.Id, p.Code, p.ClientId, p.StatusId, p.IsFailed, Issued = p.InvoiceLines.Any(l => l.InvoicedParcelId != null),
                Elsewhere = p.InvoiceLines.Where(l => l.InvoiceId != invoice.Id).Select(l => l.Invoice.Code).FirstOrDefault(),
                PaidOut = p.CodPayouts.Where(l => l.PaidOutParcelId != null).Select(l => l.Payout.Code).FirstOrDefault(),
            }).ToDictionaryAsync(p => p.Id, cancellationToken);
        foreach (var parcelId in ids)
        {
            var parcel = checks.GetValueOrDefault(parcelId) ?? throw BusinessException.NotFound($"Colis introuvable : {parcelId}", new { reason = "parcel-unknown", parcelId });
            if (parcel.ClientId != invoice.ClientId) throw BusinessException.Conflict($"Le colis {parcel.Code} appartient à un autre e-commerçant.", new { reason = "parcel-other-client", code = parcel.Code });
            if (!InvoiceRows.IsBillableParcel(parcel.StatusId, parcel.IsFailed))
            {
                throw BusinessException.Conflict($"Le colis {parcel.Code} n'est pas facturable.", new { reason = "parcel-not-billable", code = parcel.Code, statusId = parcel.StatusId });
            }
            if (parcel.Issued) throw BusinessException.Conflict($"Le colis {parcel.Code} figure déjà sur une facture émise.", new { reason = "parcel-already-invoiced", code = parcel.Code });
            if (parcel.Elsewhere is { } other)
            {
                throw BusinessException.Conflict($"Le colis {parcel.Code} figure déjà sur la facture {other} : retirez-le de celle-ci d’abord.",
                    new { reason = "parcel-on-other-invoice", code = parcel.Code, invoiceCode = other });
            }
            if (invoice.BillingType == BillingType.NORMAL && parcel.PaidOut is { } payout)
            {
                throw BusinessException.Conflict($"Le COD du colis {parcel.Code} a déjà été reversé par le bon de versement {payout} : une facture en régime normal ne peut pas l'inclure une seconde fois.",
                    new { reason = "parcel-cod-already-paid-out", code = parcel.Code, payoutCode = payout });
            }
        }
        var parcels = await InvoiceRows.MoneyOf(db.Parcels.AsNoTracking().Where(p => ids.Contains(p.Id))).ToListAsync(cancellationToken);
        var vatRate = await InvoiceDrafts.VatRateAsync(db, cancellationToken);
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            foreach (var parcel in parcels)
            {
                var amounts = InvoiceRows.LineAmounts(parcel.Billable, invoice.IsPro ? 0 : vatRate, invoice.BillingType, parcel.ChequePaid);
                var line = await db.InvoiceLines.FirstOrDefaultAsync(l => l.InvoiceId == invoice.Id && l.ParcelId == parcel.Id, cancellationToken);
                if (line is null) db.InvoiceLines.Add(InvoiceRows.LineOf(invoice.Id, parcel.Id, parcel.Code, parcel.StatusId, amounts));
                else InvoiceRows.Apply(line, parcel.StatusId, amounts);
            }
            await db.SaveChangesAsync(cancellationToken);
            await InvoiceDrafts.RefreshTotalsAsync(db, invoice.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        db.ChangeTracker.Clear();
        return await InvoiceRows.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}
