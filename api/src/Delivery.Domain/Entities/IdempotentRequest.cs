using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class IdempotentRequest
{
    public string Id { get; set; } = Cuid.New();
    public string UserId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
