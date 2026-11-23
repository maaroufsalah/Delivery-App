using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class LabelJob
{
    public string Id { get; set; } = Cuid.New();
    public string RequestedById { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public int ParcelCount { get; set; }
    public LabelJobStatus Status { get; set; } = LabelJobStatus.PENDING;
    public string? StorageKey { get; set; }
    public int? SizeBytes { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
