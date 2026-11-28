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

/// <summary>`DELETE /payments/:id` : une facture soldée qui ne l'est plus redevient émise.</summary>
public sealed record DeletePaymentCommand(string Id) : IRequest<PaymentIdDto>;

internal sealed class DeletePaymentHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<DeletePaymentCommand, PaymentIdDto>
{
    public async Task<PaymentIdDto> Handle(DeletePaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await InvoiceRows.Payments(db.Payments.AsNoTracking(), caller).Where(p => p.Id == request.Id).Select(p => new { p.Id, p.InvoiceId, p.AgencyId, p.ImportedAt }).FirstOrDefaultAsync(cancellationToken)
                      ?? throw BusinessException.NotFound("Règlement introuvable.");
        ImportedDocument.EnsureEditable(payment.ImportedAt, "Ce règlement");
        await ReturnRows.AssertAgencyReachableAsync(db, caller, payment.AgencyId, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Payments.Where(p => p.Id == payment.Id).ExecuteDeleteAsync(cancellationToken);
        if (payment.InvoiceId is { } invoiceId)
        {
            var invoice = await db.Invoices.AsNoTracking().Where(i => i.Id == invoiceId)
                .Select(i => new { i.Id, i.Status, i.TotalDue, Paid = i.Payments.Select(p => p.Amount).ToList(), Parcels = i.Lines.Select(l => l.ParcelId).ToList() }).FirstOrDefaultAsync(cancellationToken);
            if (invoice is not null && invoice.Status == InvoiceStatus.PAID && invoice.Paid.Sum(a => Money.ToCents(a)) < Math.Abs(Money.ToCents(invoice.TotalDue)))
            {
                await db.Invoices.Where(i => i.Id == invoice.Id).ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, InvoiceStatus.ISSUED).SetProperty(i => i.PaidAt, (DateTime?)null), cancellationToken);
                await db.Parcels.Where(p => invoice.Parcels.Contains(p.Id)).ExecuteUpdateAsync(s => s.SetProperty(p => p.BillingState, BillingState.INVOICED)
                    .SetProperty(p => p.FinancialStatus, FinancialStatus.FACTURE), cancellationToken);
            }
        }
        await transaction.CommitAsync(cancellationToken);
        return new PaymentIdDto(request.Id);
    }
}
