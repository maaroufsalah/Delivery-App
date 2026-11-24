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

/// <summary>`GET /invoices/export` (« Factures ») et `GET /invoices-pro/export` (« Factures clients pro », trois colonnes de plus).</summary>
public sealed record ExportInvoicesQuery(InvoiceListFilter Filter, bool Pro) : IRequest<FileContent>;

internal sealed class ExportInvoicesHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportInvoicesQuery, FileContent>
{
    public async Task<FileContent> Handle(ExportInvoicesQuery request, CancellationToken cancellationToken)
    {
        var where = InvoiceLists.Where(db, caller, request.Filter);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        var rows = await InvoiceRows.Project(InvoiceLists.Ordered(where, request.Filter.Paging)).ToListAsync(cancellationToken);
        return await Exports.ToXlsxAsync(db, excel, clock, caller, request.Pro ? "Factures clients pro" : "Factures", request.Pro ? InvoiceLists.ProColumns : InvoiceLists.Columns, rows,
            cancellationToken);
    }
}
