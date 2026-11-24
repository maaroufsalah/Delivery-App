using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using FluentValidation;

namespace Delivery.Application.Dashboard;

/// <summary>`dashboardFiltersSchema` : la query string brute (non coupée), validée par <see cref="DashboardValidator{T}"/>.</summary>
public sealed record DashboardFilters(string? Period, string? From, string? To, string? AgencyId, string? CityId, string? CourierId, string? ClientId)
{
    /// <summary>Les valeurs une fois passées par `z.string().trim()` et le défaut `month`.</summary>
    public DashboardFilters Clean() => new(Period ?? "month", Zod.Trim(From), Zod.Trim(To), Zod.Trim(AgencyId), Zod.Trim(CityId), Zod.Trim(CourierId), Zod.Trim(ClientId));

    public DashboardFilters WithClient(string clientId) => this with { ClientId = clientId };
}

/// <summary>Une requête du tableau de bord : ses filtres communs.</summary>
public interface IDashboardRequest
{
    DashboardFilters Filters { get; }
}

/// <summary>Les règles de `dashboardFiltersSchema`, dans l'ordre des clés zod ; les schémas étendus ajoutent les leurs après.</summary>
public abstract class DashboardValidator<T> : ZodValidator<T>
    where T : IDashboardRequest
{
    protected DashboardValidator()
    {
        RuleFor(x => x.Filters.Period).OneOf(Periods.All).OverridePropertyName("period");
        Day(x => x.Filters.From, "from");
        Day(x => x.Filters.To, "to");
        Id(x => x.Filters.AgencyId, "agencyId");
        Id(x => x.Filters.CityId, "cityId");
        Id(x => x.Filters.CourierId, "courierId");
        Id(x => x.Filters.ClientId, "clientId");
    }

    private void Day(System.Linq.Expressions.Expression<Func<T, string?>> field, string path) =>
        RuleFor(field).Must(v => v is null || DashboardOptions.Day.IsMatch(v.Trim())).WithMessage("invalid-date").OverridePropertyName(path);

    private void Id(System.Linq.Expressions.Expression<Func<T, string?>> field, string path)
    {
        RuleFor(field).Must(v => v is null || v.Trim().Length >= 1).WithMessage(Zod.TooSmallString(1)).OverridePropertyName(path);
        RuleFor(field).Must(v => v is null || v.Trim().Length <= 64).WithMessage(Zod.TooBigString(64)).OverridePropertyName(path);
    }

    /// <summary>`z.enum(values).default(fallback)` : absent passe, toute autre valeur doit figurer.</summary>
    protected void OneOfRule(System.Linq.Expressions.Expression<Func<T, string?>> field, string path, IReadOnlyCollection<string> values) =>
        RuleFor(field).OneOf(values).OverridePropertyName(path);

    /// <summary>`page` / `pageSize` : `z.coerce.number().int().min(1)` (`.max(100)` pour la taille).</summary>
    protected void Paged(System.Linq.Expressions.Expression<Func<T, string?>> page, System.Linq.Expressions.Expression<Func<T, string?>> pageSize)
    {
        RuleFor(page).Custom((raw, context) =>
        {
            if (DashboardPaging.Issue(raw, 1, null) is { } message) context.AddFailure("page", message);
        });
        RuleFor(pageSize).Custom((raw, context) =>
        {
            if (DashboardPaging.Issue(raw, 1, 100) is { } message) context.AddFailure("pageSize", message);
        });
    }
}

/// <summary>`z.coerce.number().int()` sur un paramètre de query string.</summary>
public static class DashboardPaging
{
    public static string? Issue(string? raw, int min, int? max)
    {
        if (raw is null) return null;
        var value = Parse(raw);
        if (double.IsNaN(value)) return "Invalid input: expected number, received NaN";
        if (Math.Floor(value) != value || double.IsInfinity(value)) return Zod.ExpectedInt;
        if (value < min) return Zod.TooSmallNumber(min);
        if (max is not null && value > max) return Zod.TooBigNumber(max.Value);
        return null;
    }

    /// <summary>La valeur validée, ou le défaut si absente.</summary>
    public static int Value(string? raw, int fallback) => raw is null ? fallback : (int)Parse(raw);

    // Number('') vaut 0, Number(' 3 ') vaut 3.
    private static double Parse(string raw)
    {
        var text = raw.Trim();
        if (text.Length == 0) return 0;
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : double.NaN;
    }
}

/// <summary>Les énumérations de `@delivery/shared` (types/dashboard.ts).</summary>
public static class DashboardOptions
{
    public static readonly Regex Day = new(@"^[0-9]{4}-[0-9]{2}-[0-9]{2}$", RegexOptions.Compiled);
    public static readonly string[] SeriesRanges = ["7d", "30d", "12m"];
    public static readonly string[] CourierSorts = ["delivered", "successRate", "codCollected", "assigned"];
    public static readonly string[] CitySorts = ["total", "successRate", "returnRate", "codCollected"];
    public static readonly string[] ClientRanks = ["volume", "cod", "successRate"];
    public static readonly string[] ExportSections = ["couriers", "cities", "clients"];
    public static readonly string[] ExportFormats = ["xlsx", "csv"];
}

/// <summary>
/// La query string du tableau de bord, liée telle quelle : `?agencyId=` reste une chaîne vide (zod la refuse),
/// au lieu du <c>null</c> qu'ASP.NET en ferait par défaut.
/// </summary>
public sealed class DashboardQueryParams
{
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Period { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? From { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? To { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? AgencyId { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? CityId { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? CourierId { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? ClientId { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Range { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Page { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? PageSize { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Sort { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Rank { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Section { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Format { get; set; }

    public DashboardFilters Filters => new(Period, From, To, AgencyId, CityId, CourierId, ClientId);
}
