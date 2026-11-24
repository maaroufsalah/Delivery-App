using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Settings;
using Delivery.Application.Settings.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using Delivery.Domain.Pricing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Delivery.Application.Public.Queries;

/// <summary>`GET /public/rates` : la grille publique depuis le siège, ville par ville.</summary>
public sealed record GetPublicRatesQuery : IRequest<IReadOnlyList<PublicRateDto>>;

public sealed record PublicRateDto(string CityId, string CityName, string ZoneName, long Price, int DeliveryDays);

internal sealed class GetPublicRatesHandler(IAppDbContext db) : IRequestHandler<GetPublicRatesQuery, IReadOnlyList<PublicRateDto>>
{
    public async Task<IReadOnlyList<PublicRateDto>> Handle(GetPublicRatesQuery request, CancellationToken cancellationToken)
    {
        await PublicCompany.CurrentAsync(db, cancellationToken);
        var headquarters = await db.Agencies.AsNoTracking().Where(a => a.DeletedAt == null).OrderBy(a => a.CreatedAt).Select(a => a.CityId).FirstOrDefaultAsync(cancellationToken);
        var cities = await db.Cities.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.ZoneId, ZoneName = c.Zone.Name, c.Zone.DeliveryDays })
            .ToListAsync(cancellationToken);
        var zones = await db.Zones.AsNoTracking().ToDictionaryAsync(z => z.Id, cancellationToken);
        var rules = await db.PricingRules.AsNoTracking().Where(r => r.IsEnabled)
            .Select(r => new { r.OriginCityId, r.DestinationCityId, r.RateDistrict, r.RateAgency, r.RateRelayPoint })
            .ToListAsync(cancellationToken);
        var originId = headquarters ?? cities.FirstOrDefault()?.Id;
        if (originId is null) return [];
        var zoneByCity = cities.ToDictionary(c => c.Id, c => c.ZoneId);
        var context = new PricingContext(
            rules.Select(r => new PriceRule(r.OriginCityId, r.DestinationCityId, Money.ToCents(r.RateDistrict), Money.ToCents(r.RateAgency), Money.ToCents(r.RateRelayPoint))).ToList(),
            [],
            id => zoneByCity.GetValueOrDefault(id),
            zoneId => zoneId is not null && zones.TryGetValue(zoneId, out var zone)
                ? (Money.ToCents(zone.BaseRate), Money.ToCents(zone.SameCityRate))
                : (DeliveryPricing.FallbackBaseRate, DeliveryPricing.FallbackSameCityRate));
        return cities.Select(c => new PublicRateDto(
            c.Id, c.Name, c.ZoneName,
            DeliveryPricing.QuoteDeliveryFee(new QuoteInput(originId, c.Id, "district"), context).Fee,
            c.Id == originId ? 0 : c.DeliveryDays)).ToList();
    }
}
