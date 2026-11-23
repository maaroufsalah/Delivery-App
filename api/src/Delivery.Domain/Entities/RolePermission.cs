using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class RolePermission
{
    public RoleName Role { get; set; }
    public string PermissionId { get; set; } = string.Empty;
    public Permission Permission { get; set; } = null!;
}
