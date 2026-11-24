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

/// <summary>`GET /invoices/:code/pdf` : masqué pour un vendeur ; identité et logo Pro pour un Client Pro.</summary>
public sealed record GetInvoicePdfQuery(string Code) : IRequest<FileContent>;

internal sealed class GetInvoicePdfHandler(IAppDbContext db, ICurrentUser caller, IPdfService pdf, IFileStorage storage) : IRequestHandler<GetInvoicePdfQuery, FileContent>
{
    public async Task<FileContent> Handle(GetInvoicePdfQuery request, CancellationToken cancellationToken)
    {
        var invoice = await CourierMasks.ForReaderAsync(db, caller, await InvoiceRows.DetailAsync(db, caller, request.Code, cancellationToken), cancellationToken);
        var (company, proLegal, proTax, logo, footer) = await InvoicePdfs.LetterheadAsync(db, storage, invoice.ClientIsPro, cancellationToken);
        var sheet = new InvoiceSheet(company, proLegal, proTax, logo, footer, invoice.Code, [InvoicePdfs.Page(invoice)]);
        return new FileContent(pdf.Render(sheet), FileContent.Pdf, $"{invoice.Code}.pdf", CacheControl: "private, no-store");
    }
}
