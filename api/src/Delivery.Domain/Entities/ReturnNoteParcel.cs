using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class ReturnNoteParcel
{
    public string ReturnNoteId { get; set; } = string.Empty;
    public string ParcelId { get; set; } = string.Empty;
    public ReturnLeg Leg { get; set; }
    public ReturnNote ReturnNote { get; set; } = null!;
    public Parcel Parcel { get; set; } = null!;
}
