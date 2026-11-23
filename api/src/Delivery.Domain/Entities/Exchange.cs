using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class Exchange
{
    public string Id { get; set; } = Cuid.New();
    public string Code { get; set; } = string.Empty;
    public ExchangeStatus Status { get; set; } = ExchangeStatus.DEMANDE;
    public string OriginalParcelId { get; set; } = string.Empty;
    public string? NewParcelId { get; set; }
    public string ReturnedProduct { get; set; } = string.Empty;
    public string NewProduct { get; set; } = string.Empty;
    public string OldDestination { get; set; } = string.Empty;
    public string NewDestination { get; set; } = string.Empty;
    public decimal Amount { get; set; } = 0m;
    public decimal Fees { get; set; } = 0m;
    public string? CourierId { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? PickedUpAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? Note { get; set; }
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Parcel OriginalParcel { get; set; } = null!;
    public Parcel? NewParcel { get; set; }
    public Courier? Courier { get; set; }
    public User? CreatedBy { get; set; }
    public ICollection<ExchangeEvent> Events { get; set; } = [];
}
