using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class ReturnHandoverEvent
{
    public string Id { get; set; } = Cuid.New();
    public string HandoverId { get; set; } = string.Empty;
    public ReturnHandoverStatus Status { get; set; }
    public string? Comment { get; set; }
    public string? AuthorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public ReturnHandover Handover { get; set; } = null!;
    public User? Author { get; set; }
}
