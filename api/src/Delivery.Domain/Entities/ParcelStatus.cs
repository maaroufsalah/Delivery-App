using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class ParcelStatus
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public ParcelStatusGroup Group { get; set; }
    public string Color { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;
    public ICollection<Parcel> Parcels { get; set; } = [];
    public ICollection<ParcelEvent> Events { get; set; } = [];
    public ICollection<ParcelTransition> TransitionsFrom { get; set; } = [];
    public ICollection<ParcelTransition> TransitionsTo { get; set; } = [];
}
