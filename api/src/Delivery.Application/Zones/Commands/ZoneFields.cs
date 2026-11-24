using Delivery.Application.Common.Validation;
using FluentValidation;

namespace Delivery.Application.Zones.Commands;

/// <summary>`zoneSchema` : montants en MAD, délai en jours.</summary>
public abstract record ZoneFields
{
    public string? Name { get; init => field = Zod.Trim(value); }

    public double? BaseRate { get; init; }

    public double? SameCityRate { get; init; }

    public double? DeliveryDays { get; init; } = 2;

    public bool? IsActive { get; init; } = true;

    public IReadOnlyList<string>? CityIds { get; init => field = value ?? []; } = [];
}

public abstract class ZoneFieldsValidator<T> : ZodValidator<T>
    where T : ZoneFields
{
    protected ZoneFieldsValidator()
    {
        RuleFor(x => x.Name).Required().MinLength(2, "too-short").MaxLength(80);
        RuleFor(x => x.BaseRate).MadAmount();
        RuleFor(x => x.SameCityRate).MadAmount();
        RuleFor(x => x.DeliveryDays).Int().Min(0, "negative").Max(30, "too-large");
        RuleForEach(x => x.CityIds).MinLength(1);
    }
}
