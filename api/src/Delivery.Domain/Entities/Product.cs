using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class Product
{
    public string Id { get; set; } = Cuid.New();
    public string ClientId { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? LengthMm { get; set; }
    public int? WidthMm { get; set; }
    public int? HeightMm { get; set; }
    public int? WeightG { get; set; }
    public decimal? Weight { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public decimal? Price { get; set; }
    public decimal? Value { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageKey { get; set; }
    public int? InternalStock { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Client Client { get; set; } = null!;
    public ICollection<StockItem> StockItems { get; set; } = [];
    public ICollection<ParcelItem> ParcelItems { get; set; } = [];
    public ICollection<StockMovement> Movements { get; set; } = [];
    public ICollection<StorageRequestItem> StorageItems { get; set; } = [];
    public ICollection<OrderLine> OrderLines { get; set; } = [];
    public ICollection<ProductAttributeAssignment> AttributeValues { get; set; } = [];
}
