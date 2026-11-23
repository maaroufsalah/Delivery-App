using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class FieldChange
{
    public string Id { get; set; } = Cuid.New();
    public string Entity { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string NewValue { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public DateTime At { get; set; }
    public User? Author { get; set; }
}
