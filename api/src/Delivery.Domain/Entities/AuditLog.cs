using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class AuditLog
{
    public string Id { get; set; } = Cuid.New();
    public string Entity { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public AuditAction Action { get; set; }
    public string? Before { get; set; }
    public string? After { get; set; }
    public List<string> Diff { get; set; } = [];
    public string? SubjectClientId { get; set; }
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? Role { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public string? RequestId { get; set; }
    public string? Source { get; set; }
    public DateTime At { get; set; }
    public User? User { get; set; }
}
