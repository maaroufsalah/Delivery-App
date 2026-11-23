using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class Announcement
{
    public string Id { get; set; } = Cuid.New();
    public string Title { get; set; } = string.Empty;
    public string? Intro { get; set; }
    public string Body { get; set; } = string.Empty;
    public AnnouncementAudience Audience { get; set; } = AnnouncementAudience.ALL;
    public string? ImageKey { get; set; }
    public string? ImageName { get; set; }
    public bool IsPublished { get; set; } = false;
    public DateTime? PublishedAt { get; set; }
    public string? CreatedById { get; set; }
    public string AuthorName { get; set; } = "Système";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public User? Author { get; set; }
}
