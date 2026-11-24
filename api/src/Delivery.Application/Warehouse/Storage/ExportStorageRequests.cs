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

/// <summary>`GET /storage-requests/export` : classeur « Entreposages ».</summary>
public sealed record ExportStorageRequestsQuery(StorageListFilter Filter) : IRequest<FileContent>;

internal sealed class ExportStorageRequestsHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportStorageRequestsQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<StorageRequestListItemDto>> Columns =
    [
        new("Demande", r => r.Code),
        new("Statut", r => r.Status),
        new("E-commerçant", r => r.ClientName),
        new("Agence", r => r.AgencyName),
        new("Références", r => r.LineCount),
        new("Quantité totale", r => r.TotalQuantity),
        new("Demandée le", r => Exports.DateTimeText(r.RequestedAt)),
        new("Décidée le", r => Exports.DateTimeText(r.DecidedAt)),
        new("Décidée par", r => r.DecidedByName ?? string.Empty),
    ];

    public async Task<FileContent> Handle(ExportStorageRequestsQuery request, CancellationToken cancellationToken)
    {
        var where = StorageRows.Filter(db.StorageRequests.AsNoTracking(), caller, request.Filter);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        var rows = (await StorageRows.Project(where).ToListAsync(cancellationToken)).Select(r => StorageRows.Fill(new StorageRequestListItemDto(), r)).ToList();
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Entreposages", Columns, rows, cancellationToken);
    }
}
