using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class ReturnHandoverItem
{
    public string Id { get; set; } = Cuid.New();
    public string HandoverId { get; set; } = string.Empty;
    public string ParcelId { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string? ConfirmedParcelId { get; set; }
    public string? ReservedParcelId { get; set; }
    public DateTime? CorrectedAt { get; set; }
    public int? CorrectedStatusId { get; set; }
    public DateTime CreatedAt { get; set; }
    public ReturnHandover Handover { get; set; } = null!;
    public Parcel Parcel { get; set; } = null!;
}
