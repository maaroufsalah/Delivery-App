using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class PushToken
{
    public string Id { get; set; } = Cuid.New();
    public string UserId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DevicePlatform Platform { get; set; }
    public string Locale { get; set; } = "fr";
    public string? AppVersion { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public User User { get; set; } = null!;
}
