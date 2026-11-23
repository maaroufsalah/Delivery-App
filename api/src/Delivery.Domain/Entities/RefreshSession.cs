namespace Delivery.Domain.Entities;

public partial class RefreshSession
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string FamilyId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public DateTime AccessExpiresAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RotatedAt { get; set; }
    public string? ReplacedById { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
