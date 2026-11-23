using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class ClientPickupAddress
{
    public string Id { get; set; } = Cuid.New();
    public string ClientId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string CityId { get; set; } = string.Empty;
    public string? DistrictId { get; set; }
    public string? Phone { get; set; }
    public bool IsDefault { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Client Client { get; set; } = null!;
    public City City { get; set; } = null!;
    public District? District { get; set; }
}
