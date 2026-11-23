namespace Delivery.Domain.Entities;

public partial class NotificationPreference
{
    public string UserId { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public DateTime UpdatedAt { get; set; }
    public User User { get; set; } = null!;
}
