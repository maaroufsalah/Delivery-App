using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Warehouse.Stock;

/// <summary>`GET /stock/export` : classeur « Stock ».</summary>
public sealed record ExportStockQuery(StockListFilter Filter) : IRequest<FileContent>;

internal sealed class ExportStockHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportStockQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<StockListItemDto>> Columns =
    [
        new("Référence", r => r.ProductReference ?? string.Empty),
        new("Produit", r => r.ProductName),
        new("E-commerçant", r => r.ClientName),
        new("Agence", r => r.AgencyName),
        new("Quantité", r => r.Quantity),
        new("Sous le seuil", r => Exports.Bool(r.LowStock)),
        new("Mis à jour", r => Exports.DateTimeText(r.UpdatedAt)),
    ];

    public async Task<FileContent> Handle(ExportStockQuery request, CancellationToken cancellationToken)
    {
        var (where, thresholdOf) = await StockRows.FilterAsync(db, caller, request.Filter, cancellationToken);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        var ordered = request.Filter.Order == "desc" ? where.OrderByDescending(s => s.Quantity) : where.OrderBy(s => s.Quantity);
        var rows = (await StockRows.Project(ordered).ToListAsync(cancellationToken)).Select(r => StockRows.Fill(new StockListItemDto(), r, thresholdOf(r.AgencyId))).ToList();
        return await Exports.ToXlsxAsync(db, excel, clock, "Stock", Columns, rows, cancellationToken);
    }
}
