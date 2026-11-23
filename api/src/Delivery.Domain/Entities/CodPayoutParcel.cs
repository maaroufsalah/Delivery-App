namespace Delivery.Domain.Entities;

public partial class CodPayoutParcel
{
    public string PayoutId { get; set; } = string.Empty;
    public string ParcelId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? PaidOutParcelId { get; set; }
    public CodPayout Payout { get; set; } = null!;
    public Parcel Parcel { get; set; } = null!;
}
