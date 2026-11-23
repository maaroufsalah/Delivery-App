using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class ReturnSlip
{
    public string Id { get; set; } = Cuid.New();
    public string Code { get; set; } = string.Empty;
    public ReturnSlipStatus Status { get; set; } = ReturnSlipStatus.DRAFT;
    public string ClientId { get; set; } = string.Empty;
    public string AgencyId { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? ReceivedByName { get; set; }
    public string? DeliveredById { get; set; }
    public string? ProofName { get; set; }
    public string? ProofMimeType { get; set; }
    public string? ProofPath { get; set; }
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Client Client { get; set; } = null!;
    public Agency Agency { get; set; } = null!;
    public User? DeliveredBy { get; set; }
    public ICollection<ReturnSlipItem> Items { get; set; } = [];
}
