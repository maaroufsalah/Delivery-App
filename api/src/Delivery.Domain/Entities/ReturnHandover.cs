using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class ReturnHandover
{
    public string Id { get; set; } = Cuid.New();
    public string Code { get; set; } = string.Empty;
    public ReturnHandoverStatus Status { get; set; } = ReturnHandoverStatus.DRAFT;
    public string CourierId { get; set; } = string.Empty;
    public string AgencyId { get; set; } = string.Empty;
    public string? Note { get; set; }
    public ReturnHandoverOrigin Origin { get; set; } = ReturnHandoverOrigin.ADMIN;
    public bool CourierCanEdit { get; set; } = false;
    public string? PickedUpById { get; set; }
    public DateTime? PickedUpAt { get; set; }
    public string? HandedOverById { get; set; }
    public DateTime? HandedOverAt { get; set; }
    public string? ConfirmedById { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? ClosedById { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? CancelledById { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Courier Courier { get; set; } = null!;
    public Agency Agency { get; set; } = null!;
    public User? ConfirmedBy { get; set; }
    public User? CreatedBy { get; set; }
    public ICollection<ReturnHandoverItem> Items { get; set; } = [];
    public ICollection<ReturnHandoverEvent> Events { get; set; } = [];
}
