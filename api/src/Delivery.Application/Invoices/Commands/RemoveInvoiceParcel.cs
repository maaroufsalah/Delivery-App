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

/// <summary>`DELETE /invoices/:code/parcels/:parcelId`.</summary>
public sealed record RemoveInvoiceParcelCommand(string Code, string ParcelId) : IRequest<InvoiceDetailDto>;

internal sealed class RemoveInvoiceParcelHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RemoveInvoiceParcelCommand, InvoiceDetailDto>
{
    public async Task<InvoiceDetailDto> Handle(RemoveInvoiceParcelCommand request, CancellationToken cancellationToken)
    {
        var invoice = await InvoiceRows.RequireDraftAsync(db, caller, request.Code, cancellationToken);
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await db.InvoiceLines.Where(l => l.InvoiceId == invoice.Id && l.ParcelId == request.ParcelId).ExecuteDeleteAsync(cancellationToken);
            await InvoiceDrafts.RefreshTotalsAsync(db, invoice.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        db.ChangeTracker.Clear();
        return await InvoiceRows.DetailAsync(db, caller, request.Code, cancellationToken);
    }
}
