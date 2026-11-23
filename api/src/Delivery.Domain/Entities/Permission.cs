using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class Permission
{
    public string Id { get; set; } = Cuid.New();
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<RolePermission> Roles { get; set; } = [];
    public ICollection<UserPermissionOverride> Overrides { get; set; } = [];
}
