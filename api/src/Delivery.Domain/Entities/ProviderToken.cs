using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class ProviderToken
{
    public string Id { get; set; } = Cuid.New();
    public string Name { get; set; } = string.Empty;
    public string CourierId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public string TokenPrefix { get; set; } = string.Empty;
    public string? PreviousTokenHash { get; set; }
    public DateTime? PreviousExpiresAt { get; set; }
    public ProviderMode Mode { get; set; } = ProviderMode.LOG_ONLY;
    public string? StatusMap { get; set; }
    public int RateLimitPerMinute { get; set; } = 120;
    public DateTime? RevokedAt { get; set; }
    public DateTime? RotatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Courier Courier { get; set; } = null!;
    public User User { get; set; } = null!;
    public ICollection<ProviderCall> Calls { get; set; } = [];
}
