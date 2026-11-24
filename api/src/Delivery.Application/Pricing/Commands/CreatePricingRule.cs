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

/// <summary>`POST /pricing-rules` : une paire de villes, refusée si déjà tarifée.</summary>
public sealed record CreatePricingRuleCommand : PricingRuleFields, IRequest<PricingRuleListItemDto>;

public sealed class CreatePricingRuleValidator : PricingRuleFieldsValidator<CreatePricingRuleCommand>;

internal sealed class CreatePricingRuleHandler(IAppDbContext db, IMapper mapper) : IRequestHandler<CreatePricingRuleCommand, PricingRuleListItemDto>
{
    public async Task<PricingRuleListItemDto> Handle(CreatePricingRuleCommand input, CancellationToken cancellationToken)
    {
        await PricingGrid.AssertCitiesExistAsync(db, [input.OriginCityId!, input.DestinationCityId!], cancellationToken);
        var clash = await db.PricingRules.Where(r => r.OriginCityId == input.OriginCityId && r.DestinationCityId == input.DestinationCityId && r.DeletedAt == null)
            .Select(r => r.Id).FirstOrDefaultAsync(cancellationToken);
        if (clash is not null) throw BusinessException.Conflict("Cette paire de villes est déjà tarifée.", new { field = "destinationCityId", id = clash });
        var rule = new PricingRule
        {
            OriginCityId = input.OriginCityId!,
            DestinationCityId = input.DestinationCityId!,
            RateDistrict = Money.FromMad(input.RateDistrict!.Value),
            RateAgency = Money.FromMad(input.RateAgency!.Value),
            RateRelayPoint = Money.FromMad(input.RateRelayPoint!.Value),
            IsEnabled = input.IsEnabled ?? true,
        };
        db.PricingRules.Add(rule);
        await db.SaveChangesAsync(cancellationToken);
        return (await mapper.ProjectTo<PricingRuleRow>(db.PricingRules.AsNoTracking().Where(r => r.Id == rule.Id)).FirstAsync(cancellationToken)).ToDto();
    }
}
