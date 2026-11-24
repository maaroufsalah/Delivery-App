using System.Globalization;
using System.Text.Json;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Invoices;

/// <summary>`GET /transfer-orders/:code/pdf` : logo de la société en tête.</summary>
public sealed record GetTransferOrderPdfQuery(string Code) : IRequest<FileContent>;

internal sealed class GetTransferOrderPdfHandler(IAppDbContext db, ICurrentUser caller, IPdfService pdf, IFileStorage storage) : IRequestHandler<GetTransferOrderPdfQuery, FileContent>
{
    public async Task<FileContent> Handle(GetTransferOrderPdfQuery request, CancellationToken cancellationToken)
    {
        var order = await TransferOrders.DetailAsync(db, caller, request.Code, cancellationToken);
        var (company, _, _, logo, _) = await InvoicePdfs.LetterheadAsync(db, storage, true, cancellationToken);
        var sheet = new TransferOrderSheet(company, logo, order.Code, order.Source, order.CreatedAt, order.CreatedByName, order.InvoiceCodes, order.TotalCollected, order.TotalFees, order.TotalDue,
            order.Lines.Select(l => new TransferOrderSheetLine(l.ClientName, l.ParcelCount, l.Collected, l.Fees, l.Due)).ToList());
        return new FileContent(pdf.Render(sheet), FileContent.Pdf, $"{TransferOrders.BaseName(order)}.pdf", CacheControl: "private, no-store");
    }
}
