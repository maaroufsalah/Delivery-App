using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class StorageRequestItem
{
    public string Id { get; set; } = Cuid.New();
    public string RequestId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; }
    public StorageRequest Request { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
