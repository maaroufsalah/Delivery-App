using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class OrderLine
{
    public string Id { get; set; } = Cuid.New();
    public string OrderId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; } = 0m;
    public DateTime CreatedAt { get; set; }
    public Order Order { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
