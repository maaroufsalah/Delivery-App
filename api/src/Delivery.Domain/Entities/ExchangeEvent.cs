using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class ExchangeEvent
{
    public string Id { get; set; } = Cuid.New();
    public string ExchangeId { get; set; } = string.Empty;
    public ExchangeStatus Status { get; set; }
    public string? Comment { get; set; }
    public string? AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public DateTime CreatedAt { get; set; }
    public Exchange Exchange { get; set; } = null!;
    public User? Author { get; set; }
}
