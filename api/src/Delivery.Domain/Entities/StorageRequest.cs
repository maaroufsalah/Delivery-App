using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class StorageRequest
{
    public string Id { get; set; } = Cuid.New();
    public string Code { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string AgencyId { get; set; } = string.Empty;
    public StorageRequestStatus Status { get; set; } = StorageRequestStatus.PENDING;
    public DateTime RequestedAt { get; set; }
    public string? Note { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecidedById { get; set; }
    public string? RefusalReason { get; set; }
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Client Client { get; set; } = null!;
    public Agency Agency { get; set; } = null!;
    public User? DecidedBy { get; set; }
    public ICollection<StorageRequestItem> Items { get; set; } = [];
    public ICollection<StockMovement> Movements { get; set; } = [];
}
