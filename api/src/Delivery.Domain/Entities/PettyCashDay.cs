using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class PettyCashDay
{
    public string Id { get; set; } = Cuid.New();
    public string AgencyId { get; set; } = string.Empty;
    public DateOnly Day { get; set; }
    public decimal OpeningBalance { get; set; } = 0m;
    public decimal Supplied { get; set; } = 0m;
    public decimal Spent { get; set; } = 0m;
    public decimal ClosingBalance { get; set; } = 0m;
    public int PendingDrafts { get; set; } = 0;
    public PettyCashDayStatus Status { get; set; } = PettyCashDayStatus.OPEN;
    public DateTime? ClosedAt { get; set; }
    public string? ClosedById { get; set; }
    public bool ClosedAutomatically { get; set; } = false;
    public DateTime? ReopenedAt { get; set; }
    public string? ReopenedById { get; set; }
    public string? ReopenReason { get; set; }
    public int ReopenCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Agency Agency { get; set; } = null!;
    public User? ClosedBy { get; set; }
    public User? ReopenedBy { get; set; }
}
