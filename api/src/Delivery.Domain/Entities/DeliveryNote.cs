using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class DeliveryNote
{
    public string Id { get; set; } = Cuid.New();
    public string Code { get; set; } = string.Empty;
    public DeliveryNoteType Type { get; set; }
    public DeliveryNoteStatus Status { get; set; } = DeliveryNoteStatus.AWAITING_VALIDATION;
    public string ClientId { get; set; } = string.Empty;
    public string AgencyId { get; set; } = string.Empty;
    public string LocationCityId { get; set; } = string.Empty;
    public DateTime? ValidatedAt { get; set; }
    public string? ValidatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Client Client { get; set; } = null!;
    public Agency Agency { get; set; } = null!;
    public City LocationCity { get; set; } = null!;
    public ICollection<DeliveryNoteParcel> Parcels { get; set; } = [];
    public ICollection<DeliveryNoteRemovedParcel> RemovedParcels { get; set; } = [];
    public ICollection<RouteNote> Routes { get; set; } = [];
    public InvoiceAdjustment? PickupFeeLine { get; set; }
    public DateTime? PickedUpAt { get; set; }
    public string? PickedUpBy { get; set; }
    public decimal? PickupFeeApplied { get; set; }
    public int? PickupThresholdApplied { get; set; }
}
