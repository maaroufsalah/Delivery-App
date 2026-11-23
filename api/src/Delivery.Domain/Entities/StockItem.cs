using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class StockItem
{
    public string Id { get; set; } = Cuid.New();
    public string ProductId { get; set; } = string.Empty;
    public string AgencyId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public int Quantity { get; set; } = 0;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Product Product { get; set; } = null!;
    public Agency Agency { get; set; } = null!;
    public Client Client { get; set; } = null!;
    public ICollection<StockMovement> Movements { get; set; } = [];
}
