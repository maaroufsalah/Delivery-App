using System.Globalization;
using System.Text.RegularExpressions;
using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.Pricing.Dtos;
using Delivery.Application.Users.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Pricing.Commands;

/// <summary>`PATCH /pricing-rules/:id`.</summary>
public sealed record UpdatePricingRuleCommand : PricingRuleFields, IRequest<PricingRuleListItemDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Id { get; init; } = string.Empty;
}

public sealed class UpdatePricingRuleValidator : PricingRuleFieldsValidator<UpdatePricingRuleCommand>;

internal sealed class UpdatePricingRuleHandler(IAppDbContext db, IMapper mapper) : IRequestHandler<UpdatePricingRuleCommand, PricingRuleListItemDto>
{
    public async Task<PricingRuleListItemDto> Handle(UpdatePricingRuleCommand input, CancellationToken cancellationToken)
    {
        var rule = await db.PricingRules.FirstOrDefaultAsync(r => r.Id == input.Id && r.DeletedAt == null, cancellationToken)
            ?? throw BusinessException.NotFound("Tarif introuvable.");
        await PricingGrid.AssertCitiesExistAsync(db, [input.OriginCityId!, input.DestinationCityId!], cancellationToken);
        rule.OriginCityId = input.OriginCityId!;
        rule.DestinationCityId = input.DestinationCityId!;
        rule.RateDistrict = Money.FromMad(input.RateDistrict!.Value);
        rule.RateAgency = Money.FromMad(input.RateAgency!.Value);
        rule.RateRelayPoint = Money.FromMad(input.RateRelayPoint!.Value);
        rule.IsEnabled = input.IsEnabled ?? true;
        await db.SaveChangesAsync(cancellationToken);
        return (await mapper.ProjectTo<PricingRuleRow>(db.PricingRules.AsNoTracking().Where(r => r.Id == rule.Id)).FirstAsync(cancellationToken)).ToDto();
    }
}
