using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Clients;

/// <summary>`clientDiscountSchema` : cibles et tarifs remisés en MAD.</summary>
public abstract record ClientDiscountFields
{
    public IReadOnlyList<string>? ZoneIds { get; init => field = value ?? []; } = [];

    public IReadOnlyList<string>? CityIds { get; init => field = value ?? []; } = [];

    public double? FlatRate { get; init; }

    public double? SameCityRate { get; init; }

    public double? PickupFee { get; init; }

    public bool? IsActive { get; init; } = true;
}

public abstract class ClientDiscountFieldsValidator<T> : ZodValidator<T>
    where T : ClientDiscountFields
{
    protected ClientDiscountFieldsValidator()
    {
        RuleForEach(x => x.ZoneIds).MinLength(1);
        RuleForEach(x => x.CityIds).MinLength(1);
        RuleFor(x => x.FlatRate).MadAmount(nullable: true);
        RuleFor(x => x.SameCityRate).MadAmount(nullable: true);
        RuleFor(x => x.PickupFee).MadAmount(nullable: true);
    }
}

/// <summary>Règles des remises (`discountTargets`, `assertNoDuplicateDiscount`).</summary>
internal static class ClientDiscounts
{
    public static decimal? Decimal(double? mad) => mad is null ? null : Money.FromMad(mad.Value);

    public static async Task<(List<string> ZoneIds, List<string> CityIds)> TargetsAsync(IAppDbContext db, ClientDiscountFields input, CancellationToken cancellationToken)
    {
        var zoneIds = input.ZoneIds!.Distinct().ToList();
        var cityIds = input.CityIds!.Distinct().ToList();
        if (cityIds.Count > 0 && await db.Cities.CountAsync(c => cityIds.Contains(c.Id) && c.DeletedAt == null, cancellationToken) != cityIds.Count)
        {
            throw BusinessException.Validation("Ville inconnue.", new { field = "cityIds" });
        }
        return (zoneIds, cityIds);
    }

    /// <summary>
    /// Une seule remise ACTIVE par cible : sous verrou consultatif (par client),
    /// une remise qui partage une ville, une zone, ou « toutes » avec une autre est refusée.
    /// </summary>
    public static async Task AssertNoDuplicateAsync(IAppDbContext db, string clientId, List<string> zoneIds, List<string> cityIds, bool isActive, string? exceptId, CancellationToken cancellationToken)
    {
        var companyId = await db.Companies.Select(c => c.Id).FirstAsync(cancellationToken);
        var lockKey = $"client-discount:{companyId}:{clientId}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({lockKey}))", cancellationToken);
        if (!isActive) return;
        var others = await db.ClientDiscounts.AsNoTracking()
            .Where(d => d.ClientId == clientId && d.IsActive && (exceptId == null || d.Id != exceptId))
            .OrderBy(d => d.CreatedAt).ThenBy(d => d.Id)
            .Select(d => new { d.Id, d.ZoneIds, Cities = d.Cities.Select(c => c.CityId).ToList(), d.FlatRate, d.SameCityRate })
            .ToListAsync(cancellationToken);
        var wantsAll = cityIds.Count == 0 && zoneIds.Count == 0;
        foreach (var other in others)
        {
            var sharedCities = cityIds.Where(other.Cities.Contains).ToList();
            var sharedZones = zoneIds.Where(other.ZoneIds.Contains).ToList();
            var bothAll = wantsAll && other.Cities.Count == 0 && other.ZoneIds.Count == 0;
            if (sharedCities.Count == 0 && sharedZones.Count == 0 && !bothAll) continue;
            var ids = other.Cities.Concat(sharedCities).Concat(other.ZoneIds).Concat(sharedZones).Distinct().ToList();
            var nameOf = await db.Cities.Where(c => ids.Contains(c.Id)).Select(c => new { c.Id, c.Name })
                .Concat(db.Zones.Where(z => ids.Contains(z.Id)).Select(z => new { z.Id, z.Name }))
                .ToDictionaryAsync(e => e.Id, e => e.Name, cancellationToken);
            string Names(IEnumerable<string> list) => string.Join(", ", list.Select(id => nameOf.GetValueOrDefault(id, id)));
            var target = sharedCities.Count > 0 ? Names(sharedCities) : sharedZones.Count > 0 ? Names(sharedZones) : "Toutes les zones";
            var label = string.Join(" · ", new[]
            {
                other.Cities.Count + other.ZoneIds.Count == 0 ? "Toutes les zones" : Names(other.Cities.Concat(other.ZoneIds)).Replace(", ", " · ", StringComparison.Ordinal),
                other.FlatRate is null ? null : $"Tarif fixe {ClientRows.FormatAmount(Money.ToCents(other.FlatRate))}",
                other.SameCityRate is null ? null : $"Tarif intra-ville {ClientRows.FormatAmount(Money.ToCents(other.SameCityRate))}",
            }.Where(part => part is not null));
            throw BusinessException.Conflict(
                $"{target} : ce client a déjà une remise active sur cette cible (« {label} »). Modifiez-la, ou désactivez-la avant d’en créer une autre.",
                new { reason = "discount-duplicate", discountId = other.Id, field = sharedCities.Count > 0 ? "cityIds" : "zoneIds" });
        }
    }
}
