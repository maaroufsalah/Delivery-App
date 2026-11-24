using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Zones.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Zones;

public sealed class ZonesProfile : Profile
{
    public ZonesProfile()
    {
        CreateProjection<Zone, ZoneRow>()
            .ForCtorParam(nameof(ZoneRow.CityIds), o => o.MapFrom(s => s.CityLinks.Select(l => l.CityId)));
        CreateProjection<ZoneRate, ZoneRateRow>();
    }
}

/// <summary>Lecture EF d'une zone ; montants convertis en centimes à la sortie.</summary>
public sealed record ZoneRow(string Id, string Name, decimal BaseRate, decimal SameCityRate, int DeliveryDays, bool IsActive, List<string> CityIds)
{
    public ZoneListItemDto ToDto() => new(Id, Name, Money.ToCents(BaseRate), Money.ToCents(SameCityRate), DeliveryDays, IsActive, CityIds, CityIds.Count);
}

public sealed record ZoneRateRow(string Id, string FromZoneId, string ToZoneId, decimal Rate, int DeliveryDays, bool IsActive)
{
    public ZoneRateItemDto ToDto() => new(Id, FromZoneId, ToZoneId, Money.ToCents(Rate), DeliveryDays, IsActive);
}

internal static class ZoneRows
{
    public static readonly IReadOnlyDictionary<string, Func<IQueryable<Zone>, bool, IOrderedQueryable<Zone>>> Sorts =
        new Dictionary<string, Func<IQueryable<Zone>, bool, IOrderedQueryable<Zone>>>
        {
            ["name"] = (q, d) => d ? q.OrderByDescending(z => z.Name) : q.OrderBy(z => z.Name),
            ["baseRate"] = (q, d) => d ? q.OrderByDescending(z => z.BaseRate) : q.OrderBy(z => z.BaseRate),
            ["sameCityRate"] = (q, d) => d ? q.OrderByDescending(z => z.SameCityRate) : q.OrderBy(z => z.SameCityRate),
            ["deliveryDays"] = (q, d) => d ? q.OrderByDescending(z => z.DeliveryDays) : q.OrderBy(z => z.DeliveryDays),
            ["isActive"] = (q, d) => d ? q.OrderByDescending(z => z.IsActive) : q.OrderBy(z => z.IsActive),
        };

    public static async Task<ZoneListItemDto> LoadAsync(IAppDbContext db, IMapper mapper, string id, CancellationToken cancellationToken)
    {
        var row = await mapper.ProjectTo<ZoneRow>(db.Zones.AsNoTracking().Where(z => z.Id == id && z.DeletedAt == null)).FirstOrDefaultAsync(cancellationToken)
            ?? throw BusinessException.NotFound("Zone introuvable.");
        return row.ToDto();
    }

    /// <summary>`knownCityIds` : toutes les villes demandées doivent exister.</summary>
    public static async Task<List<string>> KnownCityIdsAsync(IAppDbContext db, IReadOnlyList<string> cityIds, CancellationToken cancellationToken)
    {
        if (cityIds.Count == 0) return [];
        var rows = await db.Cities.Where(c => cityIds.Contains(c.Id) && c.DeletedAt == null).Select(c => c.Id).ToListAsync(cancellationToken);
        if (rows.Count != cityIds.Count)
        {
            var known = rows.ToHashSet();
            throw BusinessException.Validation("Villes inconnues dans la sélection.", new { field = "cityIds", unknown = cityIds.Where(c => !known.Contains(c)).ToList() });
        }
        return rows;
    }
}
