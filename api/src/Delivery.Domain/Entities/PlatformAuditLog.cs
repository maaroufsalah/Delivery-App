using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class PlatformAuditLog
{
    public string Id { get; set; } = Cuid.New();
    public string? ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? TargetCompanyId { get; set; }
    public string TargetCompanyName { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? Ip { get; set; }
    public DateTime CreatedAt { get; set; }
    public User? Actor { get; set; }
}
