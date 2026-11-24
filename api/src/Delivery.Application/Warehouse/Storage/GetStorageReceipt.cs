using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.Warehouse.Stock;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Warehouse.Storage;

/// <summary>`GET /storage-requests/:code/receipt` : le bon de réception PDF.</summary>
public sealed record GetStorageReceiptQuery(string Code) : IRequest<FileContent>;

internal sealed class GetStorageReceiptHandler(IAppDbContext db, ICurrentUser caller, IPdfService pdf) : IRequestHandler<GetStorageReceiptQuery, FileContent>
{
    public async Task<FileContent> Handle(GetStorageReceiptQuery request, CancellationToken cancellationToken)
    {
        var detail = await StorageRows.DetailAsync(db, caller, request.Code, cancellationToken);
        var company = await PdfCompany.LoadAsync(db, "Bon de réception", cancellationToken);
        var receipt = new StorageReceipt(company, detail.Code, detail.AgencyName, detail.ClientName, detail.Status, detail.DecidedAt, detail.DecidedByName, detail.RefusalReason,
            detail.Items.Select(i => new StorageReceiptLine(i.ProductReference, i.ProductName, i.Quantity)).ToList(), detail.LineCount, detail.TotalQuantity);
        return new FileContent(pdf.Render(receipt), FileContent.Pdf, $"{detail.Code}.pdf", CacheControl: "private, no-store");
    }
}
