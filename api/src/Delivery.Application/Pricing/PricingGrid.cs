using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Pricing.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Pricing;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Pricing;

/// <summary>
/// La grille « par origine » (`byOrigin`, `saveOrigin` de `PricingRulesService`),
/// lue par l'écran, l'export et l'import.
/// </summary>
internal static class PricingGrid
{
    private static long Effective(PricingOriginRowDto row) => row.Rate ?? row.ZoneRate;

    private static readonly Dictionary<string, Comparison<PricingOriginRowDto>> OriginSorts = new()
    {
        ["name"] = (a, b) => CityNames.CompareFr(a.Name, b.Name),
        ["code"] = (a, b) => string.CompareOrdinal(a.Code, b.Code),
        ["zoneName"] = (a, b) => CityNames.CompareFr(a.ZoneName, b.ZoneName),
        ["rate"] = (a, b) => Effective(a).CompareTo(Effective(b)),
        ["deliveryDelay"] = (a, b) => CityNames.CompareFr(a.DeliveryDelay ?? string.Empty, b.DeliveryDelay ?? string.Empty),
    };

    public static async Task<PricingOriginPageDto> ByOriginAsync(
        IAppDbContext db, string? originCityId, ListQuery query, IReadOnlyList<string>? zoneIds, CancellationToken cancellationToken)
    {
        var cities = await db.Cities.AsNoTracking().Where(c => c.DeletedAt == null && c.IsActive)
            .Select(c => new { c.Id, c.Name, c.NameAr, c.Code, c.ZoneId, c.DeliveryDelay, ZoneName = c.Zone.Name })
            .ToListAsync(cancellationToken);
        var zones = await db.Zones.AsNoTracking().ToDictionaryAsync(z => z.Id, cancellationToken);

        var origin = originCityId is not null ? cities.FirstOrDefault(c => c.Id == originCityId) : null;
        if (originCityId is null)
        {
            var headquartersCity = await db.Agencies.AsNoTracking().Where(a => a.DeletedAt == null).OrderBy(a => a.CreatedAt)
                .Select(a => a.CityId).FirstOrDefaultAsync(cancellationToken);
            origin = cities.FirstOrDefault(c => c.Id == headquartersCity)
                ?? cities.OrderBy(c => c.Name, Comparer<string>.Create(CityNames.CompareFr)).FirstOrDefault();
        }
        if (origin is null) throw BusinessException.NotFound("Ville d’origine introuvable.");

        var rules = await db.PricingRules.AsNoTracking()
            .Where(r => r.OriginCityId == origin.Id && r.DeletedAt == null && r.IsEnabled)
            .Select(r => new { r.DestinationCityId, r.RateDistrict })
            .ToListAsync(cancellationToken);
        var rateOf = new Dictionary<string, long>();
        foreach (var rule in rules) rateOf[rule.DestinationCityId] = Money.ToCents(rule.RateDistrict);
        var zoneOf = cities.ToDictionary(c => c.Id, c => c.ZoneId);
        var context = new PricingContext([], [], id => zoneOf.GetValueOrDefault(id), zoneId =>
            zoneId is not null && zones.TryGetValue(zoneId, out var zone)
                ? (Money.ToCents(zone.BaseRate), Money.ToCents(zone.SameCityRate))
                : (DeliveryPricing.FallbackBaseRate, DeliveryPricing.FallbackSameCityRate));

        var rows = cities.Select(c => new PricingOriginRowDto(
            c.Id, c.Name, c.NameAr, c.Code, c.ZoneId, c.ZoneName,
            rateOf.TryGetValue(c.Id, out var rate) ? rate : null,
            DeliveryPricing.QuoteDeliveryFee(new QuoteInput(origin.Id, c.Id, "district"), context).Fee,
            c.DeliveryDelay)).ToList();

        if (zoneIds is { Count: > 0 }) rows = rows.Where(r => zoneIds.Contains(r.ZoneId)).ToList();
        var search = query.Search?.Trim() ?? string.Empty;
        if (search.Length > 0)
        {
            var needle = CityNames.CityKey(search);
            var code = search.ToUpperInvariant();
            rows = rows.Where(r => (needle.Length > 0 && CityNames.CityKey(r.Name).Contains(needle, StringComparison.Ordinal))
                                   || r.Code == code
                                   || (r.NameAr ?? string.Empty).Contains(search, StringComparison.Ordinal)).ToList();
        }
        var sort = query.Sort ?? "name";
        if (!OriginSorts.TryGetValue(sort, out var compare))
        {
            throw BusinessException.Validation($"Tri inconnu : {sort}", new { field = "sort", allowed = OriginSorts.Keys.ToList() });
        }
        var direction = query.Descending ? -1 : 1;
        var byName = OriginSorts["name"];
        var sorted = rows.Order(Comparer<PricingOriginRowDto>.Create((a, b) =>
        {
            var order = direction * compare(a, b);
            return order != 0 ? order : byName(a, b);
        })).ToList();
        var page = sorted.Skip(query.Skip).Take(query.PageSize).ToList();
        return new PricingOriginPageDto(page, sorted.Count, query.Page, query.PageSize, new OriginRefDto(origin.Id, origin.Name, origin.Code), rateOf.Count);
    }

    /// <summary>`saveOrigin` : pose, retire ou laisse chaque tarif ; met à jour le délai affiché.</summary>
    public static async Task<PricingOriginSaveResult> SaveOriginAsync(
        IAppDbContext db, IClock clock, string originCityId, IReadOnlyList<PricingOriginRowInput> input, CancellationToken cancellationToken)
    {
        var destinations = input.Select(r => r.DestinationCityId!).Distinct().ToList();
        await AssertCitiesExistAsync(db, [originCityId, .. destinations], cancellationToken);
        var existing = await db.PricingRules.Where(r => r.OriginCityId == originCityId && destinations.Contains(r.DestinationCityId)).ToListAsync(cancellationToken);
        var ruleOf = new Dictionary<string, PricingRule>();
        foreach (var rule in existing) ruleOf[rule.DestinationCityId] = rule;
        var cities = await db.Cities.Where(c => destinations.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);

        int priced = 0, cleared = 0, delays = 0;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var row in input)
        {
            ruleOf.TryGetValue(row.DestinationCityId!, out var rule);
            var live = rule is { DeletedAt: null, IsEnabled: true };
            if (row.Rate is null)
            {
                if (rule is not null && live)
                {
                    rule.DeletedAt = clock.UtcNow;
                    rule.IsEnabled = false;
                    cleared++;
                }
            }
            else if (rule is null || !live || Money.ToCents(rule.RateDistrict) != Money.MadToCents(row.Rate.Value))
            {
                var amount = Money.FromMad(row.Rate.Value);
                if (rule is null)
                {
                    rule = new PricingRule { OriginCityId = originCityId, DestinationCityId = row.DestinationCityId! };
                    db.PricingRules.Add(rule);
                    ruleOf[row.DestinationCityId!] = rule;
                }
                rule.RateDistrict = amount;
                rule.RateAgency = amount;
                rule.RateRelayPoint = amount;
                rule.IsEnabled = true;
                rule.DeletedAt = null;
                priced++;
            }
            if (row.HasDelay && cities.TryGetValue(row.DestinationCityId!, out var city))
            {
                var delay = string.IsNullOrEmpty(row.DeliveryDelay) ? null : row.DeliveryDelay;
                if (city.DeliveryDelay != delay)
                {
                    city.DeliveryDelay = delay;
                    delays++;
                }
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PricingOriginSaveResult(priced, cleared, delays);
    }

    public static async Task AssertCitiesExistAsync(IAppDbContext db, IReadOnlyList<string> cityIds, CancellationToken cancellationToken)
    {
        var unique = cityIds.Distinct().ToList();
        if (await db.Cities.CountAsync(c => unique.Contains(c.Id) && c.DeletedAt == null, cancellationToken) != unique.Count)
        {
            throw BusinessException.Validation("Ville inconnue dans la grille.", new { field = "originCityId" });
        }
    }
}
