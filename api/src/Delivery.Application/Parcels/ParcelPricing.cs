using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Pricing;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Parcels;

/// <summary>`PricingService.quote` : la règle ville à ville, les remises du client, sinon la zone.</summary>
public static class ParcelPricing
{
    public static async Task<Quote> QuoteAsync(IAppDbContext db, string originCityId, string destinationCityId, string deliveryTarget, string? clientId, CancellationToken cancellationToken)
    {
        var rule = await db.PricingRules.AsNoTracking()
            .Where(r => r.OriginCityId == originCityId && r.DestinationCityId == destinationCityId && r.IsEnabled)
            .FirstOrDefaultAsync(cancellationToken);
        var zones = await db.Zones.AsNoTracking().ToDictionaryAsync(z => z.Id, cancellationToken);
        var cities = await db.Cities.AsNoTracking().Where(c => c.Id == originCityId || c.Id == destinationCityId)
            .Select(c => new { c.Id, c.ZoneId }).ToDictionaryAsync(c => c.Id, c => c.ZoneId, cancellationToken);
        var discounts = clientId is null
            ? []
            : await db.ClientDiscounts.AsNoTracking().Where(d => d.ClientId == clientId && d.IsActive)
                .OrderBy(d => d.CreatedAt).ThenBy(d => d.Id)
                .Select(d => new PriceDiscount(d.ClientId, d.ZoneIds, d.Cities.Select(c => c.CityId).ToList(),
                    d.FlatRate == null ? null : Money.ToCents(d.FlatRate), d.SameCityRate == null ? null : Money.ToCents(d.SameCityRate), d.IsActive))
                .ToListAsync(cancellationToken);
        IReadOnlyList<PriceRule> rules = rule is null
            ? []
            : [new PriceRule(rule.OriginCityId, rule.DestinationCityId, Money.ToCents(rule.RateDistrict), Money.ToCents(rule.RateAgency), Money.ToCents(rule.RateRelayPoint))];
        var context = new PricingContext(rules, discounts, id => cities.GetValueOrDefault(id), zoneId =>
            zoneId is not null && zones.TryGetValue(zoneId, out Zone? zone)
                ? (Money.ToCents(zone.BaseRate), Money.ToCents(zone.SameCityRate))
                : (DeliveryPricing.FallbackBaseRate, DeliveryPricing.FallbackSameCityRate));
        return DeliveryPricing.QuoteDeliveryFee(new QuoteInput(originCityId, destinationCityId, deliveryTarget, clientId), context);
    }
}
