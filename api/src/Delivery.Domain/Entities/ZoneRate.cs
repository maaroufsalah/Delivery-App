using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class ZoneRate
{
    public string Id { get; set; } = Cuid.New();
    public string FromZoneId { get; set; } = string.Empty;
    public string ToZoneId { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public int DeliveryDays { get; set; } = 2;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Zone FromZone { get; set; } = null!;
    public Zone ToZone { get; set; } = null!;
}
