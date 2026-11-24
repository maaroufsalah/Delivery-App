namespace Delivery.Application.Claims;

public record ClaimListItemDto
{
    public string Id { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Status { get; init; } = "open";
    public string Subject { get; init; } = string.Empty;
    public string TypeId { get; init; } = string.Empty;
    public string TypeLabel { get; init; } = string.Empty;
    public string TypeCode { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string ClientName { get; init; } = string.Empty;
    public string? ParcelCode { get; init; }
    public string? AssigneeId { get; init; }
    public string? AssigneeName { get; init; }
    public string AuthorName { get; init; } = string.Empty;
    public DateTime? DueAt { get; init; }
    public bool IsLate { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed record ClaimAttachmentDto(string Id, string Filename, string MimeType, int SizeBytes, DateTime CreatedAt);

public sealed record ClaimEventDto(string Id, string? Status, string? Body, bool IsInternal, string AuthorName, IReadOnlyList<ClaimAttachmentDto> Attachments, DateTime CreatedAt);

public sealed record ClaimDetailDto : ClaimListItemDto
{
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<ClaimAttachmentDto> Attachments { get; init; } = [];
    public IReadOnlyList<ClaimEventDto> Events { get; init; } = [];
    public IReadOnlyList<string> AllowedTransitions { get; init; } = [];
}

public sealed record ClaimCountersDto(int Open, int InProgress, int Late);

public sealed record ClaimTypeDto(string Id, string Code, string Label, string SubjectType, int SortOrder, bool IsActive);

public sealed record ClaimIdDto(string Id);
