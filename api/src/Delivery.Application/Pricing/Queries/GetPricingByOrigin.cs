using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Pricing.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Pricing.Queries;

/// <summary>`GET /pricing-rules/by-origin` : toutes les destinations actives d'une origine, tarifées ou non.</summary>
public sealed record GetPricingByOriginQuery(ListQuery Query, string? OriginCityId, IReadOnlyList<string>? ZoneIds) : IRequest<PricingOriginPageDto>;

internal sealed class GetPricingByOriginHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetPricingByOriginQuery, PricingOriginPageDto>
{
    public Task<PricingOriginPageDto> Handle(GetPricingByOriginQuery request, CancellationToken cancellationToken)
    {
        caller.AssertCanSeeInternalPricing("Règles de tarification");
        return PricingGrid.ByOriginAsync(db, string.IsNullOrEmpty(request.OriginCityId) ? null : request.OriginCityId, request.Query, request.ZoneIds, cancellationToken);
    }
}
