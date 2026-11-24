using AutoMapper;
using AutoMapper.QueryableExtensions;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Districts;

/// <summary>`DistrictListItem`.</summary>
public sealed class DistrictListItemDto
{
    public string Id { get; set; } = string.Empty;

    public string CityId { get; set; } = string.Empty;

    public string CityName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int ParcelCount { get; set; }
}

public sealed class DistrictsProfile : Profile
{
    public DistrictsProfile()
    {
        CreateProjection<District, DistrictListItemDto>()
            .ForMember(d => d.CityName, o => o.MapFrom(s => s.City.Name))
            .ForMember(d => d.ParcelCount, o => o.MapFrom(s => s.Parcels.Count));
    }
}

/// <summary>`districtSchema`.</summary>
public abstract record DistrictFields
{
    public string? CityId { get; init; }

    public string? Name { get; init => field = Zod.Trim(value); }

    public bool? IsActive { get; init; } = true;
}

public abstract class DistrictFieldsValidator<T> : ZodValidator<T>
    where T : DistrictFields
{
    protected DistrictFieldsValidator()
    {
        RuleFor(x => x.CityId).Required().MinLength(1, "required");
        RuleFor(x => x.Name).Required().MinLength(2, "too-short").MaxLength(80);
    }
}

internal static class DistrictRows
{
    public static readonly IReadOnlyDictionary<string, Func<IQueryable<District>, bool, IOrderedQueryable<District>>> Sorts =
        new Dictionary<string, Func<IQueryable<District>, bool, IOrderedQueryable<District>>>
        {
            ["name"] = (q, d) => d ? q.OrderByDescending(x => x.Name) : q.OrderBy(x => x.Name),
            ["cityName"] = (q, d) => d ? q.OrderByDescending(x => x.City.Name) : q.OrderBy(x => x.City.Name),
            ["isActive"] = (q, d) => d ? q.OrderByDescending(x => x.IsActive) : q.OrderBy(x => x.IsActive),
        };

    public static async Task<DistrictListItemDto> LoadAsync(IAppDbContext db, IMapper mapper, string id, CancellationToken cancellationToken) =>
        await db.Districts.AsNoTracking().Where(d => d.Id == id && d.DeletedAt == null)
            .ProjectTo<DistrictListItemDto>(mapper.ConfigurationProvider).FirstOrDefaultAsync(cancellationToken)
        ?? throw BusinessException.NotFound("Quartier introuvable.");

    public static async Task AssertCityExistsAsync(IAppDbContext db, string cityId, CancellationToken cancellationToken)
    {
        if (!await db.Cities.AnyAsync(c => c.Id == cityId && c.DeletedAt == null, cancellationToken))
        {
            throw BusinessException.Validation("Ville inconnue.", new { field = "cityId" });
        }
    }

    /// <summary>Nom unique dans la ville, sans tenir compte de la casse.</summary>
    public static async Task AssertNameFreeAsync(IAppDbContext db, string cityId, string name, string? exceptId, CancellationToken cancellationToken)
    {
        var clash = await db.Districts.AnyAsync(
            d => d.CityId == cityId && d.Name.ToLower() == name.ToLower() && d.DeletedAt == null && (exceptId == null || d.Id != exceptId), cancellationToken);
        if (clash) throw BusinessException.Conflict($"« {name} » existe déjà dans cette ville.", new { field = "name" });
    }
}
