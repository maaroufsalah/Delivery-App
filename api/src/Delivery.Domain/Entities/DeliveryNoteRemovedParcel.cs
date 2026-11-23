namespace Delivery.Domain.Entities;

public partial class DeliveryNoteRemovedParcel
{
    public string DeliveryNoteId { get; set; } = string.Empty;
    public string ParcelId { get; set; } = string.Empty;
    public string? RouteId { get; set; }
    public DateTime RemovedAt { get; set; }
    public string? RemovedBy { get; set; }
    public DateTime? RestoredAt { get; set; }
    public string? RestoredBy { get; set; }
    public DeliveryNote DeliveryNote { get; set; } = null!;
    public Parcel Parcel { get; set; } = null!;
}
