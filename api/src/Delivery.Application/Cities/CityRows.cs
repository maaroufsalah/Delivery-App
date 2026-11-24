using AutoMapper;
using AutoMapper.QueryableExtensions;
using Delivery.Application.Cities.Dtos;
using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Cities;

public sealed class CitiesProfile : Profile
{
    public CitiesProfile()
    {
        CreateProjection<City, CityListItemDto>()
            .ForMember(d => d.ZoneName, o => o.MapFrom(s => s.Zone.Name))
            .ForMember(d => d.DistrictCount, o => o.MapFrom(s => s.Districts.Count))
            .ForMember(d => d.ParcelCount, o => o.Ignore())
            .ForMember(d => d.CodeLocked, o => o.Ignore());
    }
}

/// <summary>Lecture partagée des villes : tri, compteurs de colis.</summary>
internal static class CityRows
{
    public static readonly IReadOnlyDictionary<string, Func<IQueryable<City>, bool, IOrderedQueryable<City>>> Sorts =
        new Dictionary<string, Func<IQueryable<City>, bool, IOrderedQueryable<City>>>
        {
            ["name"] = (q, d) => d ? q.OrderByDescending(c => c.Name) : q.OrderBy(c => c.Name),
            ["code"] = (q, d) => d ? q.OrderByDescending(c => c.Code) : q.OrderBy(c => c.Code),
            ["zoneName"] = (q, d) => d ? q.OrderByDescending(c => c.Zone.Name) : q.OrderBy(c => c.Zone.Name),
            ["isActive"] = (q, d) => d ? q.OrderByDescending(c => c.IsActive) : q.OrderBy(c => c.IsActive),
            ["createdAt"] = (q, d) => d ? q.OrderByDescending(c => c.CreatedAt) : q.OrderBy(c => c.CreatedAt),
        };

    /// <summary>Colis au départ ET à l'arrivée de chaque ville (un colis intra-ville compte deux fois, comme Speedy).</summary>
    public static async Task<Dictionary<string, int>> ParcelCountsAsync(IAppDbContext db, IReadOnlyCollection<string> cityIds, CancellationToken cancellationToken)
    {
        var counts = new Dictionary<string, int>();
        if (cityIds.Count == 0) return counts;
        var origins = await db.Parcels.Where(p => cityIds.Contains(p.OriginCityId)).GroupBy(p => p.OriginCityId)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        var destinations = await db.Parcels.Where(p => cityIds.Contains(p.DestinationCityId)).GroupBy(p => p.DestinationCityId)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        foreach (var row in origins.Concat(destinations)) counts[row.Key] = counts.GetValueOrDefault(row.Key) + row.Count;
        return counts;
    }

    public static async Task<List<CityListItemDto>> WithCountsAsync(IAppDbContext db, List<CityListItemDto> rows, CancellationToken cancellationToken)
    {
        var counts = await ParcelCountsAsync(db, rows.Select(r => r.Id).ToList(), cancellationToken);
        foreach (var row in rows)
        {
            row.ParcelCount = counts.GetValueOrDefault(row.Id);
            row.CodeLocked = row.ParcelCount > 0;
        }
        return rows;
    }

    public static async Task<CityListItemDto> LoadAsync(IAppDbContext db, IMapper mapper, string id, CancellationToken cancellationToken)
    {
        var row = await db.Cities.AsNoTracking().Where(c => c.Id == id && c.DeletedAt == null)
            .ProjectTo<CityListItemDto>(mapper.ConfigurationProvider).FirstOrDefaultAsync(cancellationToken)
            ?? throw BusinessException.NotFound("Ville introuvable.");
        return (await WithCountsAsync(db, [row], cancellationToken))[0];
    }

    public static async Task AssertZoneExistsAsync(IAppDbContext db, string zoneId, CancellationToken cancellationToken)
    {
        if (!await db.Zones.AnyAsync(z => z.Id == zoneId && z.DeletedAt == null, cancellationToken))
        {
            throw BusinessException.Validation("Zone inconnue.", new { field = "zoneId" });
        }
    }

    public static async Task AssertCodeFreeAsync(IAppDbContext db, string code, string? exceptId, CancellationToken cancellationToken)
    {
        if (await db.Cities.AnyAsync(c => c.Code == code && c.DeletedAt == null && (exceptId == null || c.Id != exceptId), cancellationToken))
        {
            throw BusinessException.Conflict($"Le code « {code} » est déjà pris.", new { field = "code" });
        }
    }
}
