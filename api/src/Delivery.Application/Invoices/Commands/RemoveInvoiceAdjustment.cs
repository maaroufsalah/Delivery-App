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

/// <summary>`DELETE /invoices/:code/adjustments/:id` : la ligne retirée, tracée au journal.</summary>
public sealed record RemoveInvoiceAdjustmentCommand(string Code, string Id) : IRequest<InvoiceDetailDto>;

internal sealed class RemoveInvoiceAdjustmentHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RemoveInvoiceAdjustmentCommand, InvoiceDetailDto>
{
    public async Task<InvoiceDetailDto> Handle(RemoveInvoiceAdjustmentCommand request, CancellationToken cancellationToken)
    {
        var invoice = await InvoiceRows.RequireDraftAsync(db, caller, request.Code, cancellationToken);
        var authorName = await InvoiceRows.AuthorNameAsync(db, caller.UserId, cancellationToken);
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            if (!await db.Invoices.AnyAsync(i => i.Id == invoice.Id && i.Status == InvoiceStatus.DRAFT, cancellationToken))
            {
                throw BusinessException.Conflict("Cette facture est émise : elle ne se modifie plus.", new { reason = "invoice-issued" });
            }
            var line = await db.InvoiceAdjustments.FirstOrDefaultAsync(a => a.Id == request.Id && a.InvoiceId == invoice.Id, cancellationToken)
                       ?? throw BusinessException.NotFound("Ligne introuvable sur cette facture.");
            db.InvoiceAdjustments.Remove(line);
            db.FieldChanges.Add(new FieldChange
            {
                Entity = "Invoice", EntityId = invoice.Id, Field = "adjustment",
                OldValue = $"{(Money.ToCents(line.Amount) / 100.0).ToString(CultureInfo.InvariantCulture)} MAD — {line.Reason}", NewValue = "retirée", Reason = line.Reason,
                AuthorId = caller.UserId, AuthorName = authorName, At = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
            await InvoiceDrafts.RefreshTotalsAsync(db, invoice.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        db.ChangeTracker.Clear();
        return await InvoiceRows.DetailAsync(db, caller, request.Code, cancellationToken);
    }
}
