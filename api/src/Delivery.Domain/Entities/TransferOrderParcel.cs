namespace Delivery.Domain.Entities;

public partial class TransferOrderParcel
{
    public string TransferOrderId { get; set; } = string.Empty;
    public string ParcelId { get; set; } = string.Empty;
    public TransferOrder TransferOrder { get; set; } = null!;
    public Parcel Parcel { get; set; } = null!;
}
