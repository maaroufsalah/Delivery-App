namespace Delivery.Application.Common.Models;

/// <summary>Forme `Paginated&lt;T&gt;` de `@delivery/shared`.</summary>
public sealed record Paginated<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public static Paginated<T> Of(IReadOnlyList<T> items, int total, ListQuery query) => new(items, total, query.Page, query.PageSize);
}

/// <summary>`listQuerySchema` de Speedy (common/crud/list-query.ts), une fois validé.</summary>
public record ListQuery
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 25;

    public string? Search { get; init; }

    public string? Sort { get; init; }

    public string Order { get; init; } = "asc";

    public string? IsActive { get; init; }

    public int Skip => (Page - 1) * PageSize;

    public bool? ActiveFilter => IsActive is null ? null : IsActive == "true";

    public bool Descending => Order == "desc";
}
