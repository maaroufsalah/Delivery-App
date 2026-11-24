using AutoMapper;
using Delivery.Application.Common.Validation;
using Delivery.Application.Pricing.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using FluentValidation;

namespace Delivery.Application.Pricing;

public sealed record PricingRuleRow(
    string Id, string OriginCityId, string OriginCityName, string DestinationCityId, string DestinationCityName,
    decimal RateDistrict, decimal RateAgency, decimal RateRelayPoint, bool IsEnabled)
{
    public PricingRuleListItemDto ToDto() => new(
        Id, OriginCityId, OriginCityName, DestinationCityId, DestinationCityName,
        Money.ToCents(RateDistrict), Money.ToCents(RateAgency), Money.ToCents(RateRelayPoint), IsEnabled);
}

public sealed class PricingProfile : Profile
{
    public PricingProfile()
    {
        CreateProjection<PricingRule, PricingRuleRow>()
            .ForCtorParam(nameof(PricingRuleRow.OriginCityName), o => o.MapFrom(s => s.OriginCity.Name))
            .ForCtorParam(nameof(PricingRuleRow.DestinationCityName), o => o.MapFrom(s => s.DestinationCity.Name));
    }
}

/// <summary>`pricingRuleSchema` : trois tarifs en MAD, un par mode de livraison.</summary>
public abstract record PricingRuleFields
{
    public string? OriginCityId { get; init; }

    public string? DestinationCityId { get; init; }

    public double? RateDistrict { get; init; }

    public double? RateAgency { get; init; }

    public double? RateRelayPoint { get; init; }

    public bool? IsEnabled { get; init; } = true;
}

public abstract class PricingRuleFieldsValidator<T> : ZodValidator<T>
    where T : PricingRuleFields
{
    protected PricingRuleFieldsValidator()
    {
        RuleFor(x => x.OriginCityId).Required().MinLength(1, "required");
        RuleFor(x => x.DestinationCityId).Required().MinLength(1, "required");
        RuleFor(x => x.RateDistrict).MadAmount();
        RuleFor(x => x.RateAgency).MadAmount();
        RuleFor(x => x.RateRelayPoint).MadAmount();
    }
}

internal static class PricingRuleSorts
{
    public static readonly IReadOnlyDictionary<string, Func<IQueryable<PricingRule>, bool, IOrderedQueryable<PricingRule>>> All =
        new Dictionary<string, Func<IQueryable<PricingRule>, bool, IOrderedQueryable<PricingRule>>>
        {
            ["originCityName"] = (q, d) => d ? q.OrderByDescending(r => r.OriginCity.Name) : q.OrderBy(r => r.OriginCity.Name),
            ["destinationCityName"] = (q, d) => d ? q.OrderByDescending(r => r.DestinationCity.Name) : q.OrderBy(r => r.DestinationCity.Name),
            ["rateDistrict"] = (q, d) => d ? q.OrderByDescending(r => r.RateDistrict) : q.OrderBy(r => r.RateDistrict),
            ["isEnabled"] = (q, d) => d ? q.OrderByDescending(r => r.IsEnabled) : q.OrderBy(r => r.IsEnabled),
        };
}
