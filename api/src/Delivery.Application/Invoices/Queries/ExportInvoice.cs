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

/// <summary>`GET /invoices/:code/export` : deux feuilles, les colis et le récapitulatif (masques du vendeur appliqués).</summary>
public sealed record ExportInvoiceQuery(string Code) : IRequest<FileContent>;

internal sealed class ExportInvoiceHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel) : IRequestHandler<ExportInvoiceQuery, FileContent>
{
    public async Task<FileContent> Handle(ExportInvoiceQuery request, CancellationToken cancellationToken)
    {
        var invoice = await CourierMasks.ForReaderAsync(db, caller, await InvoiceRows.DetailAsync(db, caller, request.Code, cancellationToken), cancellationToken);
        Exports.AssertWithinLimit(invoice.Lines.Count);
        var banner = await Exports.BannerAsync(db, cancellationToken);
        var content = excel.WriteSheets(InvoiceSheets.Build(invoice, banner));
        var name = $"Facture-{System.Text.RegularExpressions.Regex.Replace(invoice.Code, "[^A-Za-z0-9._-]", "-")}.xlsx";
        return new FileContent(content, FileContent.Xlsx, name, CacheControl: "private, no-store");
    }
}
