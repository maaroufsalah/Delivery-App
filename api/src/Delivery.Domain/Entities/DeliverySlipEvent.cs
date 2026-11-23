using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class DeliverySlipEvent
{
    public string Id { get; set; } = Cuid.New();
    public string SlipId { get; set; } = string.Empty;
    public DeliverySlipStatus Status { get; set; }
    public string? Comment { get; set; }
    public string? AuthorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DeliverySlip Slip { get; set; } = null!;
    public User? Author { get; set; }
}
