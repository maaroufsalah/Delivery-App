using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class UserRole
{
    public string UserId { get; set; } = string.Empty;
    public RoleName Role { get; set; }
    public User User { get; set; } = null!;
}
