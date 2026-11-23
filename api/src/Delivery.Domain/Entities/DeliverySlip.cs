using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class DeliverySlip
{
    public string Id { get; set; } = Cuid.New();
    public string ParcelId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string? CourierId { get; set; }
    public DeliverySlipStatus Status { get; set; } = DeliverySlipStatus.COLLECTED;
    public string? Reference { get; set; }
    public string? CollectedById { get; set; }
    public DateTime? CollectedAt { get; set; }
    public string? HandedOverById { get; set; }
    public DateTime? HandedOverAt { get; set; }
    public string? ReceivedById { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public string? ReturnedById { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public string? ReturnedToName { get; set; }
    public string? ClosedById { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Parcel Parcel { get; set; } = null!;
    public Client Client { get; set; } = null!;
    public Courier? Courier { get; set; }
    public ICollection<BlNoteItem> NoteItems { get; set; } = [];
    public ICollection<DeliverySlipEvent> Events { get; set; } = [];
}
