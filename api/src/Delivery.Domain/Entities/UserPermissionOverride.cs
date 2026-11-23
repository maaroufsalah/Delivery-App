using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class UserPermissionOverride
{
    public string Id { get; set; } = Cuid.New();
    public string UserId { get; set; } = string.Empty;
    public string PermissionId { get; set; } = string.Empty;
    public bool Granted { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedById { get; set; }
    public User User { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
