using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class ClientDocument
{
    public string Id { get; set; } = Cuid.New();
    public string ClientId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ClientDocumentKind Kind { get; set; } = ClientDocumentKind.OTHER;
    public string StorageKey { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public int SizeBytes { get; set; }
    public string? UploadedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Client Client { get; set; } = null!;
    public User? UploadedBy { get; set; }
}
