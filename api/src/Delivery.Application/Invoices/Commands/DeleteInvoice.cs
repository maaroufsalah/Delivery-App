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

/// <summary>`DELETE /invoices/:code` : un brouillon sans ligne hors colis.</summary>
public sealed record DeleteInvoiceCommand(string Code) : IRequest<ReturnCodeDto>;

internal sealed class DeleteInvoiceHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<DeleteInvoiceCommand, ReturnCodeDto>
{
    public async Task<ReturnCodeDto> Handle(DeleteInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await InvoiceRows.Scoped(db.Invoices.AsNoTracking(), caller).Where(i => i.Code == request.Code).Select(i => new { i.Id, i.Status, Adjustments = i.Adjustments.Count })
                          .FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound($"Facture introuvable : {request.Code}");
        if (invoice.Status != InvoiceStatus.DRAFT) throw BusinessException.Conflict("Une facture émise ne se supprime pas.", new { reason = "invoice-issued" });
        if (invoice.Adjustments > 0)
        {
            throw BusinessException.Conflict("Ce brouillon porte des lignes hors colis : retirez-les d’abord, une à une.", new { reason = "invoice-has-adjustments", count = invoice.Adjustments });
        }
        await db.Invoices.Where(i => i.Id == invoice.Id).ExecuteDeleteAsync(cancellationToken);
        return new ReturnCodeDto(request.Code);
    }
}
