namespace Delivery.Domain.Entities;

public partial class CourierCashSessionParcel
{
    public string SessionId { get; set; } = string.Empty;
    public string ParcelId { get; set; } = string.Empty;
    public decimal Cash { get; set; } = 0m;
    public decimal Cheque { get; set; } = 0m;
    public decimal Collected { get; set; } = 0m;
    public decimal Commission { get; set; } = 0m;
    public DateTime AddedAt { get; set; }
    public string? SettledParcelId { get; set; }
    public CourierCashSession Session { get; set; } = null!;
    public Parcel Parcel { get; set; } = null!;
}
