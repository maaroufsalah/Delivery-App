using System.Text.RegularExpressions;
using Delivery.Application.Common.Validation;
using FluentValidation;

namespace Delivery.Application.Cities.Commands;

/// <summary>Champs de `citySchema`, transformés comme zod (code rogné, en majuscules).</summary>
public abstract record CityFields
{
    public string? Name { get; init => field = Zod.Trim(value); }

    public string? NameAr { get; init => field = Zod.Trim(value); } = string.Empty;

    public string? Code { get; init => field = value?.Trim().ToUpperInvariant(); }

    public string? ZoneId { get; init; }

    public bool? IsActive { get; init; } = true;
}

public abstract class CityFieldsValidator<T> : ZodValidator<T>
    where T : CityFields
{
    private static readonly Regex CodePattern = new("^[A-Z]{2,3}$", RegexOptions.Compiled);

    protected CityFieldsValidator()
    {
        RuleFor(x => x.Name).Required().MinLength(2, "too-short").MaxLength(80);
        RuleFor(x => x.NameAr).MaxLength(120, "too-large");
        RuleFor(x => x.Code).Required().Pattern(CodePattern, "invalid-city-code");
        RuleFor(x => x.ZoneId).Required().MinLength(1, "required");
    }
}
