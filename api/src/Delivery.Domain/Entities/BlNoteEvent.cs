using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class BlNoteEvent
{
    public string Id { get; set; } = Cuid.New();
    public string NoteId { get; set; } = string.Empty;
    public BlNoteStatus Status { get; set; }
    public string? Comment { get; set; }
    public string? AuthorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public BlNote Note { get; set; } = null!;
    public User? Author { get; set; }
}
