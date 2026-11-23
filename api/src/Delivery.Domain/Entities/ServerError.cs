using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class ServerError
{
    public string Id { get; set; } = Cuid.New();
    public DateTime At { get; set; }
    public string Source { get; set; } = "api";
    public string? RequestId { get; set; }
    public string? Method { get; set; }
    public string Route { get; set; } = string.Empty;
    public int Status { get; set; }
    public string? Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Stack { get; set; }
    public int? DurationMs { get; set; }
    public string? UserId { get; set; }
    public string? Role { get; set; }
    public string? TenantId { get; set; }
    public string? Ip { get; set; }
}
