using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class BlNoteItem
{
    public string Id { get; set; } = Cuid.New();
    public string NoteId { get; set; } = string.Empty;
    public string SlipId { get; set; } = string.Empty;
    public string? ReservedSlipId { get; set; }
    public DateTime CreatedAt { get; set; }
    public BlNote Note { get; set; } = null!;
    public DeliverySlip Slip { get; set; } = null!;
}
