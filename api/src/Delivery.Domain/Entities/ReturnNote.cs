using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class ReturnNote
{
    public string Id { get; set; } = Cuid.New();
    public string Code { get; set; } = string.Empty;
    public ReturnNoteStatus Status { get; set; } = ReturnNoteStatus.DRAFT;
    public string ClientId { get; set; } = string.Empty;
    public string? CourierId { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Client Client { get; set; } = null!;
    public Courier? Courier { get; set; }
    public ICollection<ReturnNoteParcel> Parcels { get; set; } = [];
}
