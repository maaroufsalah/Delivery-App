using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Warehouse.Products;

/// <summary>`ProductListItem`.</summary>
public class ProductListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? LengthMm { get; set; }
    public int? WidthMm { get; set; }
    public int? HeightMm { get; set; }
    public int? WeightG { get; set; }
    public long? Price { get; set; }
    public long? DeclaredValue { get; set; }
    public string? ImageUrl { get; set; }
    public int? InternalStock { get; set; }
    public int StockTotal { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed record ProductAttributeValueItemDto(string Id, string AttributeId, string AttributeName, string Value);

public sealed record ProductStockDto(string AgencyId, string AgencyName, int Quantity, bool LowStock);

/// <summary>`ProductDetail`.</summary>
public sealed class ProductDetailDto : ProductListItemDto
{
    [System.Text.Json.Serialization.JsonPropertyOrder(1)]
    public List<ProductAttributeValueItemDto> Attributes { get; set; } = [];

    [System.Text.Json.Serialization.JsonPropertyOrder(1)]
    public List<ProductStockDto> Stock { get; set; } = [];
}

public sealed record ProductAttributeValueDto(string Id, string Value);

public sealed record ProductAttributeItemDto(string Id, string Name, IReadOnlyList<ProductAttributeValueDto> Values);

/// <summary>Ligne EF d'un produit, avant conversion « fil ».</summary>
public sealed class ProductRow
{
    public string Id { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? LengthMm { get; set; }
    public int? WidthMm { get; set; }
    public int? HeightMm { get; set; }
    public int? WeightG { get; set; }
    public decimal? Price { get; set; }
    public decimal? Value { get; set; }
    public string? ImageUrl { get; set; }
    public int? InternalStock { get; set; }
    public int StockTotal { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class WarehouseProfile : Profile
{
    public WarehouseProfile()
    {
        CreateProjection<Product, ProductRow>()
            .ForMember(d => d.ClientName, o => o.MapFrom(s => s.Client.Name))
            .ForMember(d => d.StockTotal, o => o.MapFrom(s => s.StockItems.Sum(i => i.Quantity)));
    }
}

public static class ProductRows
{
    private static T Fill<T>(T dto, ProductRow r)
        where T : ProductListItemDto
    {
        dto.Id = r.Id;
        dto.ClientId = r.ClientId;
        dto.ClientName = r.ClientName;
        dto.Reference = r.Reference;
        dto.Name = r.Name;
        dto.LengthMm = r.LengthMm;
        dto.WidthMm = r.WidthMm;
        dto.HeightMm = r.HeightMm;
        dto.WeightG = r.WeightG;
        dto.Price = Money.ToCentsOrNull(r.Price);
        dto.DeclaredValue = Money.ToCentsOrNull(r.Value);
        dto.ImageUrl = r.ImageUrl;
        dto.InternalStock = r.InternalStock;
        dto.StockTotal = r.StockTotal;
        dto.IsActive = r.IsActive;
        dto.CreatedAt = r.CreatedAt;
        return dto;
    }

    public static async Task<List<ProductListItemDto>> ListAsync(IMapper mapper, IQueryable<Product> products, CancellationToken cancellationToken) =>
        (await mapper.ProjectTo<ProductRow>(products).ToListAsync(cancellationToken)).Select(r => Fill(new ProductListItemDto(), r)).ToList();

    public static async Task<ProductDetailDto> DetailAsync(IAppDbContext db, ICurrentUser caller, IMapper mapper, string id, CancellationToken cancellationToken)
    {
        var scoped = WarehouseScope.Products(db.Products.AsNoTracking(), caller).Where(p => p.Id == id && p.DeletedAt == null);
        var row = await mapper.ProjectTo<ProductRow>(scoped).FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound("Produit introuvable.");
        var threshold = await WarehouseScope.LowStockThresholdAsync(db, null, cancellationToken);
        var detail = Fill(new ProductDetailDto(), row);
        detail.Attributes = await db.ProductAttributeAssignments.AsNoTracking().Where(a => a.ProductId == id)
            .Select(a => new ProductAttributeValueItemDto(a.Value.Id, a.Value.Attribute.Id, a.Value.Attribute.Name, a.Value.Value)).ToListAsync(cancellationToken);
        detail.Stock = (await db.StockItems.AsNoTracking().Where(s => s.ProductId == id).OrderBy(s => s.Agency.Name)
            .Select(s => new { s.AgencyId, AgencyName = s.Agency.Name, s.Quantity }).ToListAsync(cancellationToken))
            .Select(s => new ProductStockDto(s.AgencyId, s.AgencyName, s.Quantity, s.Quantity <= threshold)).ToList();
        return detail;
    }

    public static async Task<IReadOnlyList<ProductAttributeItemDto>> AttributesAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var rows = await db.ProductAttributes.AsNoTracking().OrderBy(a => a.Name)
            .Select(a => new { a.Id, a.Name, Values = a.Values.OrderBy(v => v.Value).Select(v => new ProductAttributeValueDto(v.Id, v.Value)).ToList() })
            .ToListAsync(cancellationToken);
        return rows.Select(a => new ProductAttributeItemDto(a.Id, a.Name, a.Values)).ToList();
    }
}
