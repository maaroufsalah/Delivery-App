using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class ReturnSlipItem
{
    public string Id { get; set; } = Cuid.New();
    public string SlipId { get; set; } = string.Empty;
    public string ParcelId { get; set; } = string.Empty;
    public ReturnReason Reason { get; set; } = ReturnReason.OTHER;
    public decimal FeeApplied { get; set; } = 0m;
    public string? DeliveredParcelId { get; set; }
    public string? ReservedParcelId { get; set; }
    public DateTime CreatedAt { get; set; }
    public ReturnSlip Slip { get; set; } = null!;
    public Parcel Parcel { get; set; } = null!;
}
