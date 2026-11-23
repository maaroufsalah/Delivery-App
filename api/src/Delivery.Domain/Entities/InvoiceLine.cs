using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class InvoiceLine
{
    public string Id { get; set; } = Cuid.New();
    public string InvoiceId { get; set; } = string.Empty;
    public string ParcelId { get; set; } = string.Empty;
    public string ParcelCode { get; set; } = string.Empty;
    public int StatusId { get; set; }
    public decimal CodAmount { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal FailureFee { get; set; } = 0m;
    public decimal VatAmount { get; set; } = 0m;
    public decimal ChequePaid { get; set; } = 0m;
    public decimal NetDue { get; set; }
    public string? InvoicedParcelId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public Parcel Parcel { get; set; } = null!;
}
