using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class ClaimEvent
{
    public string Id { get; set; } = Cuid.New();
    public string ClaimId { get; set; } = string.Empty;
    public ClaimStatus? Status { get; set; }
    public string? Body { get; set; }
    public bool IsInternal { get; set; } = false;
    public string? AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Claim Claim { get; set; } = null!;
    public User? Author { get; set; }
    public ICollection<ClaimAttachment> Attachments { get; set; } = [];
}
