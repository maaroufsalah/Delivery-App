using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class Notification
{
    public string Id { get; set; } = Cuid.New();
    public string UserId { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string Params { get; set; } = "{}";
    public string Data { get; set; } = "{}";
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public User User { get; set; } = null!;
}
