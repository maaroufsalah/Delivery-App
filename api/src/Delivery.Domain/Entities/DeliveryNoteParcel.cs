namespace Delivery.Domain.Entities;

public partial class DeliveryNoteParcel
{
    public string DeliveryNoteId { get; set; } = string.Empty;
    public string ParcelId { get; set; } = string.Empty;
    public DateTime? CorrectedAt { get; set; }
    public int? CorrectedStatusId { get; set; }
    public DeliveryNote DeliveryNote { get; set; } = null!;
    public Parcel Parcel { get; set; } = null!;
}
