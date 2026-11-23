using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class PricingRule
{
    public string Id { get; set; } = Cuid.New();
    public string OriginCityId { get; set; } = string.Empty;
    public string DestinationCityId { get; set; } = string.Empty;
    public decimal RateDistrict { get; set; }
    public decimal RateAgency { get; set; }
    public decimal RateRelayPoint { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public City OriginCity { get; set; } = null!;
    public City DestinationCity { get; set; } = null!;
}
