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

/// <summary>`StockListItem`.</summary>
public class StockListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? ProductReference { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string AgencyId { get; set; } = string.Empty;
    public string AgencyName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public bool LowStock { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed record StockMovementItemDto(string Id, string Kind, int Quantity, int Balance, string? Reason, string? StorageRequestCode, string? ParcelCode, string? AuthorName, DateTime CreatedAt);

/// <summary>`StockDetail` : la ligne et ses 100 derniers mouvements.</summary>
public sealed class StockDetailDto : StockListItemDto
{
    [System.Text.Json.Serialization.JsonPropertyOrder(1)]
    public List<StockMovementItemDto> Movements { get; set; } = [];
}

/// <summary>`stockListQuerySchema` une fois lu.</summary>
public sealed record StockListFilter(ListQuery Paging, string Order, IReadOnlyList<string>? ClientId, IReadOnlyList<string>? AgencyId, string? LowStock);

/// <summary>`StockService.move` : un mouvement verrouillé, refusé s'il rend le stock négatif.</summary>
public static class StockMoves
{
    public static async Task<int> MoveAsync(IAppDbContext db, string productId, string agencyId, int quantity, StockMovementKind kind, string? reason, string? storageRequestId,
        string? parcelId, string? authorId, CancellationToken cancellationToken)
    {
        if (quantity == 0) throw BusinessException.Validation("Un mouvement de zéro ne s’enregistre pas.");
        var product = await db.Products.AsNoTracking().Where(p => p.Id == productId && p.DeletedAt == null).Select(p => new { p.Id, p.ClientId, p.Name }).FirstOrDefaultAsync(cancellationToken)
                      ?? throw BusinessException.NotFound("Produit introuvable.");
        await db.Database.SqlQuery<string>($"""SELECT id AS "Value" FROM stock_items WHERE "productId" = {productId} AND "agencyId" = {agencyId} FOR UPDATE""").ToListAsync(cancellationToken);
        var item = await db.StockItems.FirstOrDefaultAsync(s => s.ProductId == productId && s.AgencyId == agencyId, cancellationToken);
        var before = item?.Quantity ?? 0;
        var balance = before + quantity;
        if (balance < 0)
        {
            throw BusinessException.Conflict($"Stock insuffisant pour « {product.Name} » : {before} en stock, {Math.Abs(quantity)} demandé(s).",
                new { reason = "insufficient-stock", productId = product.Id, available = before, requested = Math.Abs(quantity) });
        }
        if (item is null)
        {
            item = new StockItem { ProductId = productId, AgencyId = agencyId, ClientId = product.ClientId, Quantity = balance };
            db.StockItems.Add(item);
        }
        else item.Quantity = balance;
        db.StockMovements.Add(new StockMovement
        {
            StockItemId = item.Id, ProductId = productId, AgencyId = agencyId, Kind = kind, Quantity = quantity, Balance = balance,
            Reason = reason, StorageRequestId = storageRequestId, ParcelId = parcelId, AuthorId = authorId,
        });
        await db.SaveChangesAsync(cancellationToken);
        return balance;
    }
}

internal static class StockRows
{
    public sealed record Row(string Id, string ProductId, string ProductName, string? ProductReference, string ClientId, string ClientName, string AgencyId, string AgencyName, int Quantity, DateTime UpdatedAt);

    public static IQueryable<Row> Project(IQueryable<StockItem> items) => items.Select(s => new Row(s.Id, s.ProductId, s.Product.Name, s.Product.Reference, s.ClientId, s.Client.Name, s.AgencyId, s.Agency.Name, s.Quantity, s.UpdatedAt));

    public static T Fill<T>(T dto, Row r, int threshold)
        where T : StockListItemDto
    {
        dto.Id = r.Id;
        dto.ProductId = r.ProductId;
        dto.ProductName = r.ProductName;
        dto.ProductReference = r.ProductReference;
        dto.ClientId = r.ClientId;
        dto.ClientName = r.ClientName;
        dto.AgencyId = r.AgencyId;
        dto.AgencyName = r.AgencyName;
        dto.Quantity = r.Quantity;
        dto.LowStock = r.Quantity <= threshold;
        dto.UpdatedAt = r.UpdatedAt;
        return dto;
    }

    public static async Task<(IQueryable<StockItem> Where, Func<string, int> ThresholdOf)> FilterAsync(IAppDbContext db, ICurrentUser caller, StockListFilter filter, CancellationToken cancellationToken)
    {
        var agencyIds = filter.AgencyId;
        var threshold = await WarehouseScope.LowStockThresholdAsync(db, agencyIds is { Count: 1 } ? agencyIds[0] : null, cancellationToken);
        var where = WarehouseScope.Stock(db.StockItems.AsNoTracking(), caller);
        var filtered = agencyIds is not null && caller.MayFilterBy(caller.AgencyId);
        Dictionary<string, int>? perAgency = null;
        if (filtered && agencyIds!.Count > 1)
        {
            perAgency = [];
            foreach (var id in agencyIds) perAgency[id] = await WarehouseScope.LowStockThresholdAsync(db, id, cancellationToken);
        }
        if (filter.ClientId is { Count: > 0 } clients && caller.ClientId is null) where = where.Where(s => clients.Contains(s.ClientId));
        if (filtered) where = where.Where(s => agencyIds!.Contains(s.AgencyId));
        if (perAgency is not null && filter.LowStock is "true" or "false")
        {
            var low = filter.LowStock == "true";
            IQueryable<StockItem>? union = null;
            foreach (var (agencyId, limit) in perAgency)
            {
                var part = low ? where.Where(s => s.AgencyId == agencyId && s.Quantity <= limit) : where.Where(s => s.AgencyId == agencyId && s.Quantity > limit);
                union = union is null ? part : union.Union(part);
            }
            where = union ?? where;
        }
        else if (filter.LowStock == "true") where = where.Where(s => s.Quantity <= threshold);
        else if (filter.LowStock == "false") where = where.Where(s => s.Quantity > threshold);
        if (!string.IsNullOrEmpty(filter.Paging.Search))
        {
            var pattern = Like.Contains(filter.Paging.Search);
            where = where.Where(s => EF.Functions.ILike(s.Product.Name, pattern) || EF.Functions.ILike(s.Product.Reference!, pattern));
        }
        return (where, id => perAgency?.GetValueOrDefault(id, threshold) ?? threshold);
    }

    public static async Task<StockDetailDto> DetailAsync(IAppDbContext db, ICurrentUser caller, string id, CancellationToken cancellationToken)
    {
        var row = await Project(WarehouseScope.Stock(db.StockItems.AsNoTracking(), caller).Where(s => s.Id == id)).FirstOrDefaultAsync(cancellationToken)
                  ?? throw BusinessException.NotFound("Ligne de stock introuvable.");
        var threshold = await WarehouseScope.LowStockThresholdAsync(db, row.AgencyId, cancellationToken);
        var detail = Fill(new StockDetailDto(), row, threshold);
        detail.Movements = (await db.StockMovements.AsNoTracking().Where(m => m.StockItemId == id).OrderByDescending(m => m.CreatedAt).Take(100)
            .Select(m => new
            {
                m.Id, m.Kind, m.Quantity, m.Balance, m.Reason, m.CreatedAt,
                Storage = m.StorageRequest != null ? m.StorageRequest.Code : null,
                Parcel = m.Parcel != null ? m.Parcel.Code : null,
                First = m.Author != null ? m.Author.FirstName : null,
                Last = m.Author != null ? m.Author.LastName : null,
            }).ToListAsync(cancellationToken))
            .Select(m => new StockMovementItemDto(m.Id, Money.ToWire(m.Kind), m.Quantity, m.Balance, m.Reason, m.Storage, m.Parcel, m.First is null ? null : $"{m.First} {m.Last}", m.CreatedAt))
            .ToList();
        return detail;
    }
}
