using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class DeliveryType
{
    public string Id { get; set; } = Cuid.New();
    public string Name { get; set; } = string.Empty;
    public decimal Multiplier { get; set; } = 1m;
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public ICollection<Parcel> Parcels { get; set; } = [];
}
