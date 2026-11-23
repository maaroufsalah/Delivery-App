using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class ClientDiscount
{
    public string Id { get; set; } = Cuid.New();
    public string ClientId { get; set; } = string.Empty;
    public List<string> ZoneIds { get; set; } = [];
    public decimal? FlatRate { get; set; }
    public decimal? SameCityRate { get; set; }
    public decimal? PickupFee { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Client Client { get; set; } = null!;
    public ICollection<ClientDiscountCity> Cities { get; set; } = [];
}
