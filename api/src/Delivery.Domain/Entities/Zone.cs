using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class Zone
{
    public string Id { get; set; } = Cuid.New();
    public string Name { get; set; } = string.Empty;
    public decimal BaseRate { get; set; }
    public decimal SameCityRate { get; set; }
    public int DeliveryDays { get; set; } = 2;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public ICollection<City> Cities { get; set; } = [];
    public ICollection<ZoneCity> CityLinks { get; set; } = [];
    public ICollection<ZoneRate> RatesFrom { get; set; } = [];
    public ICollection<ZoneRate> RatesTo { get; set; } = [];
}
