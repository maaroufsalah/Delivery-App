using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class ClaimAttachment
{
    public string Id { get; set; } = Cuid.New();
    public string ClaimId { get; set; } = string.Empty;
    public string? EventId { get; set; }
    public string Filename { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public int SizeBytes { get; set; }
    public string? UploadedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public Claim Claim { get; set; } = null!;
    public ClaimEvent? Event { get; set; }
    public User? UploadedBy { get; set; }
}
