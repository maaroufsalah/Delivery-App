namespace Delivery.Application.Announcements;

/// <summary>`AnnouncementListItem` (liste) et `AnnouncementDetail` (avec le corps).</summary>
public record AnnouncementListItemDto
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Intro { get; init; }
    public string Audience { get; init; } = "all";
    public bool IsPublished { get; init; }
    public DateTime? PublishedAt { get; init; }
    public bool HasImage { get; init; }
    public string? ImageName { get; init; }
    public string AuthorName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed record AnnouncementDetailDto : AnnouncementListItemDto
{
    public string Body { get; init; } = string.Empty;
}

public sealed record AnnouncementIdDto(string Id);
