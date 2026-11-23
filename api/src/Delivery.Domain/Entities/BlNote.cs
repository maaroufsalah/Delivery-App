using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class BlNote
{
    public string Id { get; set; } = Cuid.New();
    public string Code { get; set; } = string.Empty;
    public BlNoteStatus Status { get; set; } = BlNoteStatus.DRAFT;
    public string CourierId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string AgencyId { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string? HandedOverById { get; set; }
    public DateTime? HandedOverAt { get; set; }
    public string? ReceivedById { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public string? ReturnedById { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public string? ReturnedToName { get; set; }
    public string? ClosedById { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Courier Courier { get; set; } = null!;
    public Client Client { get; set; } = null!;
    public Agency Agency { get; set; } = null!;
    public User? CreatedBy { get; set; }
    public ICollection<BlNoteItem> Items { get; set; } = [];
    public ICollection<BlNoteEvent> Events { get; set; } = [];
}
