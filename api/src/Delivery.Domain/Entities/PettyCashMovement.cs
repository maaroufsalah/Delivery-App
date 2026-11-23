using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class PettyCashMovement
{
    public string Id { get; set; } = Cuid.New();
    public string AgencyId { get; set; } = string.Empty;
    public PettyCashKind Kind { get; set; }
    public DateTime OccurredAt { get; set; }
    public decimal Amount { get; set; }
    public string? Label { get; set; }
    public string? Category { get; set; }
    public string? BuyerId { get; set; }
    public string? BuyerName { get; set; }
    public string? Note { get; set; }
    public string? ProofName { get; set; }
    public string? ProofMimeType { get; set; }
    public string? ProofPath { get; set; }
    public PettyCashStatus Status { get; set; } = PettyCashStatus.VALIDATED;
    public string? RefusalReason { get; set; }
    public string? DecidedById { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Agency Agency { get; set; } = null!;
    public User? Buyer { get; set; }
    public User? CreatedBy { get; set; }
    public User? DecidedBy { get; set; }
}
