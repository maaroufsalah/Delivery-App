using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class ParcelEvent
{
    public string Id { get; set; } = Cuid.New();
    public string ParcelId { get; set; } = string.Empty;
    public int StatusId { get; set; }
    public string? Comment { get; set; }
    public string? AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public string? CityId { get; set; }
    public string? AttachmentName { get; set; }
    public string? AttachmentPath { get; set; }
    public string? AttachmentMimeType { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public bool IsDispute { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public Parcel Parcel { get; set; } = null!;
    public ParcelStatus Status { get; set; } = null!;
    public User? Author { get; set; }
    public City? City { get; set; }
}
