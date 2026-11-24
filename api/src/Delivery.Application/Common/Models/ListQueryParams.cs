using System.Globalization;
using Delivery.Domain.Common;

namespace Delivery.Application.Common.Models;

/// <summary>
/// Query string brute de `listQuerySchema` : les nombres sont « coercés » comme
/// `z.coerce.number()`, les erreurs rendues au format 422 de zod.
/// </summary>
public sealed class ListQueryParams
{
    public string? Page { get; set; }

    public string? PageSize { get; set; }

    public string? Search { get; set; }

    public string? Sort { get; set; }

    public string? Order { get; set; }

    public string? IsActive { get; set; }

    public ListQuery ToQuery() => ToQueryWithDefault(25);

    public ListQuery ToQueryWithDefault(int defaultPageSize)
    {
        var issues = new List<object>();
        var page = Coerce(Page, 1, 1, null, "page", issues);
        var pageSize = Coerce(PageSize, defaultPageSize, 1, 200, "pageSize", issues);
        var search = Search?.Trim();
        if (search is { Length: > 120 }) issues.Add(new { path = "search", message = "Too big: expected string to have <=120 characters" });
        var sort = Sort?.Trim();
        if (sort is { Length: > 40 }) issues.Add(new { path = "sort", message = "Too big: expected string to have <=40 characters" });
        var order = Order ?? "asc";
        if (order is not ("asc" or "desc")) issues.Add(new { path = "order", message = "Invalid option: expected one of \"asc\"|\"desc\"" });
        if (IsActive is not null and not ("true" or "false")) issues.Add(new { path = "isActive", message = "Invalid option: expected one of \"true\"|\"false\"" });
        if (issues.Count > 0) throw BusinessException.Validation("Saisie invalide.", new { issues });
        return new ListQuery
        {
            Page = page,
            PageSize = pageSize,
            Search = string.IsNullOrEmpty(Search) ? null : search,
            Sort = string.IsNullOrEmpty(Sort) ? null : sort,
            Order = order,
            IsActive = IsActive,
        };
    }

    private static int Coerce(string? raw, int fallback, int min, int? max, string path, List<object> issues)
    {
        if (raw is null) return fallback;
        // Number('') vaut 0, Number(' 3 ') vaut 3.
        var text = raw.Trim();
        double value;
        if (text.Length == 0) value = 0;
        else if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            issues.Add(new { path, message = "Invalid input: expected number, received NaN" });
            return fallback;
        }
        if (value != Math.Floor(value) || double.IsInfinity(value))
        {
            issues.Add(new { path, message = "Invalid input: expected int, received number" });
            return fallback;
        }
        if (value < min)
        {
            issues.Add(new { path, message = $"Too small: expected number to be >={min}" });
            return fallback;
        }
        if (max is not null && value > max)
        {
            issues.Add(new { path, message = $"Too big: expected number to be <={max}" });
            return fallback;
        }
        return (int)value;
    }
}

public static class MultiValue
{
    /// <summary>`asList` / `splitMultiValue` : répété ou séparé par des virgules, sans doublon.</summary>
    public static IReadOnlyList<string>? AsList(IEnumerable<string>? values)
    {
        if (values is null) return null;
        var parts = values.SelectMany(v => (v ?? string.Empty).Split(','))
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return parts.Count > 0 ? parts : null;
    }
}

public static class Sorting
{
    /// <summary>`orderByOf` : une clé inconnue est un 422 qui liste les clés admises.</summary>
    public static IOrderedQueryable<T> OrderByKey<T>(
        IQueryable<T> source, ListQuery query, IReadOnlyDictionary<string, Func<IQueryable<T>, bool, IOrderedQueryable<T>>> columns, string fallback)
    {
        string key;
        if (query.Sort is not null)
        {
            if (!columns.ContainsKey(query.Sort))
            {
                throw BusinessException.Validation($"Tri inconnu : {query.Sort}", new { field = "sort", allowed = columns.Keys.ToList() });
            }
            key = query.Sort;
        }
        else
        {
            key = fallback;
        }
        return columns[key](source, query.Order == "desc");
    }
}
