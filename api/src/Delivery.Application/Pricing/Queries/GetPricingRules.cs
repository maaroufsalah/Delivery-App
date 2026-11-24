using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Pricing.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Pricing.Queries;

/// <summary>`GET /pricing-rules` : la grille brute, paire par paire.</summary>
public sealed record GetPricingRulesQuery(ListQuery Query, string? OriginCityId, string? DestinationCityId) : IRequest<Paginated<PricingRuleListItemDto>>;

internal sealed class GetPricingRulesHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<GetPricingRulesQuery, Paginated<PricingRuleListItemDto>>
{
    public async Task<Paginated<PricingRuleListItemDto>> Handle(GetPricingRulesQuery request, CancellationToken cancellationToken)
    {
        caller.AssertCanSeeInternalPricing("Règles de tarification");
        var where = db.PricingRules.AsNoTracking().Where(r => r.DeletedAt == null);
        if (request.Query.ActiveFilter is { } enabled) where = where.Where(r => r.IsEnabled == enabled);
        if (!string.IsNullOrEmpty(request.OriginCityId)) where = where.Where(r => r.OriginCityId == request.OriginCityId);
        if (!string.IsNullOrEmpty(request.DestinationCityId)) where = where.Where(r => r.DestinationCityId == request.DestinationCityId);
        if (!string.IsNullOrEmpty(request.Query.Search))
        {
            var pattern = Like.Contains(request.Query.Search);
            where = where.Where(r => EF.Functions.ILike(r.OriginCity.Name, pattern) || EF.Functions.ILike(r.DestinationCity.Name, pattern));
        }
        var total = await where.CountAsync(cancellationToken);
        var rows = await mapper.ProjectTo<PricingRuleRow>(Sorting.OrderByKey(where, request.Query, PricingRuleSorts.All, "originCityName")
            .Skip(request.Query.Skip).Take(request.Query.PageSize)).ToListAsync(cancellationToken);
        return Paginated<PricingRuleListItemDto>.Of(rows.Select(r => r.ToDto()).ToList(), total, request.Query);
    }
}
