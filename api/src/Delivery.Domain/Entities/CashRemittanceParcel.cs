namespace Delivery.Domain.Entities;

public partial class CashRemittanceParcel
{
    public string RemittanceId { get; set; } = string.Empty;
    public string ParcelId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Cash { get; set; } = 0m;
    public decimal Cheque { get; set; } = 0m;
    public decimal Commission { get; set; } = 0m;
    public string? ConfirmedParcelId { get; set; }
    public CashRemittance Remittance { get; set; } = null!;
    public Parcel Parcel { get; set; } = null!;
}
