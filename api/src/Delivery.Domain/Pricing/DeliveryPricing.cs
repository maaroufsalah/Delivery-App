namespace Delivery.Domain.Pricing;

/// <summary>Une ligne de la grille (`PricingRule` de `@delivery/shared/pricing.ts`), en centimes.</summary>
public sealed record PriceRule(string OriginCityId, string DestinationCityId, long District, long Agency, long RelayPoint);

/// <summary>Remise client par ville ou par zone, en centimes.</summary>
public sealed record PriceDiscount(string ClientId, IReadOnlyList<string> ZoneIds, IReadOnlyList<string> CityIds, long? FlatRate, long? SameCityRate, bool IsActive)
{
    /// <summary>`discountTarget` : ville, zone, ou toutes.</summary>
    public string Target => CityIds.Count > 0 ? "city" : ZoneIds.Count > 0 ? "zone" : "all";
}

public sealed record QuoteInput(string OriginCityId, string DestinationCityId, string DeliveryTarget, string? ClientId = null, double? Multiplier = null);

public sealed record Quote(long Fee, long BaseFee, bool Matched, bool Discounted, bool SameCity);

public sealed class PricingContext(
    IReadOnlyList<PriceRule> rules,
    IReadOnlyList<PriceDiscount> discounts,
    Func<string, string?> zoneOfCity,
    Func<string?, (long BaseRate, long SameCityRate)> zoneRates)
{
    public IReadOnlyList<PriceRule> Rules { get; } = rules;

    public IReadOnlyList<PriceDiscount> Discounts { get; } = discounts;

    public Func<string, string?> ZoneOfCity { get; } = zoneOfCity;

    public Func<string?, (long BaseRate, long SameCityRate)> ZoneRates { get; } = zoneRates;
}

/// <summary>Le calcul du tarif de livraison (`quoteDeliveryFee`, DOMAIN.md §3.1).</summary>
public static class DeliveryPricing
{
    /// <summary>Tarifs de repli d'une ville sans zone connue : 40 MAD, 20 MAD en intra-ville.</summary>
    public const long FallbackBaseRate = 4000;
    public const long FallbackSameCityRate = 2000;

    public static Quote QuoteDeliveryFee(QuoteInput input, PricingContext context)
    {
        var sameCity = input.OriginCityId == input.DestinationCityId;
        var destinationZoneId = context.ZoneOfCity(input.DestinationCityId);
        var rule = context.Rules.FirstOrDefault(r => r.OriginCityId == input.OriginCityId && r.DestinationCityId == input.DestinationCityId);
        var fallback = context.ZoneRates(destinationZoneId);
        var baseFee = rule is not null
            ? input.DeliveryTarget switch
            {
                "agency" => rule.Agency,
                "relay-point" => rule.RelayPoint,
                _ => rule.District,
            }
            : sameCity ? fallback.SameCityRate : fallback.BaseRate;

        var active = input.ClientId is null
            ? []
            : context.Discounts.Where(d => d.IsActive && d.ClientId == input.ClientId).ToList();
        var discount = active.FirstOrDefault(d => d.CityIds.Contains(input.DestinationCityId))
            ?? active.FirstOrDefault(d => destinationZoneId is not null && d.ZoneIds.Contains(destinationZoneId))
            ?? active.FirstOrDefault(d => d.Target == "all");
        long? @override = discount is null ? null : sameCity ? discount.SameCityRate ?? discount.FlatRate : discount.FlatRate;
        var multiplier = input.Multiplier ?? 1;
        var fee = JsRound((@override ?? baseFee) * multiplier);
        return new Quote(fee, baseFee, rule is not null, @override is not null, sameCity);
    }

    /// <summary>`Math.round` de JS : le demi va vers +∞.</summary>
    public static long JsRound(double value) => (long)Math.Floor(value + 0.5);
}

/// <summary>`PricingPolicy` : politique commerciale réglée par les paramètres (centimes).</summary>
public sealed record PricingPolicy(long FailureFeeSameCity, long FailureFeeCrossCity, long PickupFee, long FreePickupThreshold)
{
    /// <summary>`DEFAULT_PRICING_POLICY`.</summary>
    public static readonly PricingPolicy Default = new(0, 1000, 2000, 5);
}

/// <summary>Frais d'échec (`failureFee`, `failureAfterTransition` de `pricing.ts`).</summary>
public static class FailureFees
{
    /// <summary>`FAILURE_FEE_STATUS_IDS` : les statuts qui constituent un échec facturable.</summary>
    public static readonly int[] StatusIds = [5, 6, 8, 9, 10, 11, 23, 24, 31, 32, 33, 34, 35, 37];

    private static readonly int[] ReturnFlowIds = [20, 21, 22, 23, 24];

    public static bool IsFailureFeeStatus(int statusId) => StatusIds.Contains(statusId);

    public static long? For(int statusId, bool sameCity, PricingPolicy policy) =>
        !IsFailureFeeStatus(statusId) ? null : sameCity ? policy.FailureFeeSameCity : policy.FailureFeeCrossCity;

    /// <summary>
    /// L'échec suit le colis dans le circuit retour ; sinon il prend l'état du statut cible.
    /// <c>Keep</c> vrai : le frais en base ne bouge pas (`undefined` côté Prisma).
    /// </summary>
    public static (bool IsFailed, long? FailureFee, bool Keep) AfterTransition(bool isFailed, long? currentFee, int targetStatusId, bool targetIsFailure, long? nextFee)
    {
        if (isFailed && ReturnFlowIds.Contains(targetStatusId))
        {
            return currentFee is null && nextFee is not null ? (true, nextFee, false) : (true, null, true);
        }
        return (targetIsFailure, nextFee, false);
    }
}
