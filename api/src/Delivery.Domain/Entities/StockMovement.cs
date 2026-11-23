using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class StockMovement
{
    public string Id { get; set; } = Cuid.New();
    public string StockItemId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string AgencyId { get; set; } = string.Empty;
    public StockMovementKind Kind { get; set; }
    public int Quantity { get; set; }
    public int Balance { get; set; }
    public string? Reason { get; set; }
    public string? StorageRequestId { get; set; }
    public string? ParcelId { get; set; }
    public string? AuthorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public StockItem StockItem { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public Agency Agency { get; set; } = null!;
    public StorageRequest? StorageRequest { get; set; }
    public Parcel? Parcel { get; set; }
    public User? Author { get; set; }
}
