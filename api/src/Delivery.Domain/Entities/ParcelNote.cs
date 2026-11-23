using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class ParcelNote
{
    public string Id { get; set; } = Cuid.New();
    public string ParcelId { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AttachmentName { get; set; }
    public DateTime CreatedAt { get; set; }
    public Parcel Parcel { get; set; } = null!;
    public User? Author { get; set; }
}
