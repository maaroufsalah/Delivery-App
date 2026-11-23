using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class BulkJob
{
    public string Id { get; set; } = Cuid.New();
    public string RequestedById { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public BulkJobStatus Status { get; set; } = BulkJobStatus.PENDING;
    public string Input { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Processed { get; set; } = 0;
    public string DoneIds { get; set; } = "[]";
    public string Failures { get; set; } = "[]";
    public string? Result { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
