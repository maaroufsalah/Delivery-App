using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class District
{
    public string Id { get; set; } = Cuid.New();
    public string CityId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public City City { get; set; } = null!;
    public ICollection<Parcel> Parcels { get; set; } = [];
    public ICollection<ClientPickupAddress> PickupAddresses { get; set; } = [];
    public ICollection<Order> Orders { get; set; } = [];
}
