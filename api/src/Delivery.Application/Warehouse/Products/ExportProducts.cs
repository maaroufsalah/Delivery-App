using System.Text.Json.Serialization;
using AutoMapper;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Persistence;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Warehouse.Products;

/// <summary>`GET /products/export` : classeur « Produits ».</summary>
public sealed record ExportProductsQuery(ProductListFilter Filter) : IRequest<FileContent>;

internal sealed class ExportProductsHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper, IExcelService excel, IClock clock) : IRequestHandler<ExportProductsQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<ProductListItemDto>> Columns =
    [
        new("Référence", r => r.Reference ?? string.Empty),
        new("Produit", r => r.Name),
        new("E-commerçant", r => r.ClientName),
        new("Prix", r => Exports.Amount(r.Price)),
        new("Valeur déclarée", r => Exports.Amount(r.DeclaredValue)),
        new("Longueur (mm)", r => r.LengthMm?.ToString() ?? string.Empty),
        new("Largeur (mm)", r => r.WidthMm?.ToString() ?? string.Empty),
        new("Hauteur (mm)", r => r.HeightMm?.ToString() ?? string.Empty),
        new("Poids (g)", r => r.WeightG?.ToString() ?? string.Empty),
        new("Stock total", r => r.StockTotal),
        new("Actif", r => Exports.Bool(r.IsActive)),
    ];

    public async Task<FileContent> Handle(ExportProductsQuery request, CancellationToken cancellationToken)
    {
        var where = GetProductsHandler.Filter(db.Products.AsNoTracking(), caller, request.Filter);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        var ordered = request.Filter.Order == "desc" ? where.OrderByDescending(p => p.Name) : where.OrderBy(p => p.Name);
        var rows = await ProductRows.ListAsync(mapper, ordered, cancellationToken);
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Produits", Columns, rows, cancellationToken);
    }
}
