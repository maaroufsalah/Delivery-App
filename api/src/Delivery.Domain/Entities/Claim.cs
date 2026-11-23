using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class Claim
{
    public string Id { get; set; } = Cuid.New();
    public string Code { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string TypeId { get; set; } = string.Empty;
    public ClaimStatus Status { get; set; } = ClaimStatus.OPEN;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ParcelId { get; set; }
    public string? ParcelCode { get; set; }
    public string? AssigneeId { get; set; }
    public DateTime? DueAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? CreatedById { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Client Client { get; set; } = null!;
    public ClaimType Type { get; set; } = null!;
    public Parcel? Parcel { get; set; }
    public User? Assignee { get; set; }
    public User? Author { get; set; }
    public ICollection<ClaimEvent> Events { get; set; } = [];
    public ICollection<ClaimAttachment> Attachments { get; set; } = [];
}
