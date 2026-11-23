using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class PasswordResetRequest
{
    public string Id { get; set; } = Cuid.New();
    public string Email { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public string MailProvider { get; set; } = "log";
    public DateTime CreatedAt { get; set; }
    public User? User { get; set; }
}
