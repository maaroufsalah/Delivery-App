using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Agencies;

/// <summary>`AgencyListItem`.</summary>
public sealed record AgencyListItemDto(
    string Id, string Name, string CityId, string CityName, string? Phone, string? Email, string? Address, string? Currency, string? Timezone,
    IReadOnlyList<string> CoveredCityIds, bool IsActive, bool IsHeadquarters, int UserCount, int ParcelCount);

public sealed record AgencyRow(
    string Id, string Name, string CityId, string CityName, string? Phone, string? Email, string? Address, string? Currency, string? Timezone,
    List<string> CoveredCityIds, bool IsActive, int UserCount, int ParcelCount)
{
    public AgencyListItemDto ToDto(string? headquartersId) =>
        new(Id, Name, CityId, CityName, Phone, Email, Address, Currency, Timezone, CoveredCityIds, IsActive, Id == headquartersId, UserCount, ParcelCount);
}

public sealed class AgenciesProfile : Profile
{
    public AgenciesProfile()
    {
        CreateProjection<Agency, AgencyRow>()
            .ForCtorParam(nameof(AgencyRow.CityName), o => o.MapFrom(s => s.City.Name))
            .ForCtorParam(nameof(AgencyRow.CoveredCityIds), o => o.MapFrom(s => s.CityLinks.Select(l => l.CityId)))
            .ForCtorParam(nameof(AgencyRow.UserCount), o => o.MapFrom(s => s.Users.Count))
            .ForCtorParam(nameof(AgencyRow.ParcelCount), o => o.MapFrom(s => s.Parcels.Count));
    }
}

/// <summary>`agencySchema`.</summary>
public abstract record AgencyFields
{
    public string? Name { get; init => field = Zod.Trim(value); }

    public string? CityId { get; init; }

    public string? Phone { get; init => field = Phones.NormalizeOptional(value); }

    public string? Email { get; init; }

    public string? Address { get; init => field = Zod.Trim(value); }

    public string? Currency { get; init => field = Zod.Trim(value); }

    public string? Timezone { get; init => field = Zod.Trim(value); }

    public IReadOnlyList<string>? CoveredCityIds { get; init => field = value ?? []; } = [];

    public bool? IsActive { get; init; } = true;
}

public abstract class AgencyFieldsValidator<T> : ZodValidator<T>
    where T : AgencyFields
{
    protected AgencyFieldsValidator()
    {
        RuleFor(x => x.Name).Required().MinLength(2, "too-short").MaxLength(80);
        RuleFor(x => x.CityId).Required().MinLength(1, "required");
        RuleFor(x => x.Phone).Must(p => string.IsNullOrEmpty(p) || Phones.Pattern.IsMatch(p)).WithMessage(Zod.InvalidInput);
        RuleFor(x => x.Email).Must(e => string.IsNullOrEmpty(e) || Zod.IsEmail(e)).WithMessage(Zod.InvalidInput);
        RuleFor(x => x.Address).Must(v => v is null || v.Length <= 255).WithMessage(Zod.InvalidInput);
        RuleFor(x => x.Currency).Must(v => v is null || v.Length <= 3).WithMessage(Zod.InvalidInput);
        RuleFor(x => x.Timezone).Must(v => v is null || v.Length <= 64).WithMessage(Zod.InvalidInput);
        RuleForEach(x => x.CoveredCityIds).MinLength(1);
    }
}

internal static class AgencyRows
{
    public static readonly IReadOnlyDictionary<string, Func<IQueryable<Agency>, bool, IOrderedQueryable<Agency>>> Sorts =
        new Dictionary<string, Func<IQueryable<Agency>, bool, IOrderedQueryable<Agency>>>
        {
            ["name"] = (q, d) => d ? q.OrderByDescending(a => a.Name) : q.OrderBy(a => a.Name),
            ["cityName"] = (q, d) => d ? q.OrderByDescending(a => a.City.Name) : q.OrderBy(a => a.City.Name),
            ["isActive"] = (q, d) => d ? q.OrderByDescending(a => a.IsActive) : q.OrderBy(a => a.IsActive),
            ["createdAt"] = (q, d) => d ? q.OrderByDescending(a => a.CreatedAt) : q.OrderBy(a => a.CreatedAt),
        };

    /// <summary>Le siège : l'agence la plus ancienne.</summary>
    public static Task<string?> HeadquartersIdAsync(IAppDbContext db, CancellationToken cancellationToken) =>
        db.Agencies.AsNoTracking().Where(a => a.DeletedAt == null).OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).Select(a => a.Id).FirstOrDefaultAsync(cancellationToken);

    public static async Task<AgencyListItemDto> LoadAsync(IAppDbContext db, IMapper mapper, string id, CancellationToken cancellationToken)
    {
        var row = await mapper.ProjectTo<AgencyRow>(db.Agencies.AsNoTracking().Where(a => a.Id == id && a.DeletedAt == null)).FirstOrDefaultAsync(cancellationToken)
            ?? throw BusinessException.NotFound("Agence introuvable.");
        return row.ToDto(await HeadquartersIdAsync(db, cancellationToken));
    }

    public static async Task AssertCityExistsAsync(IAppDbContext db, string cityId, CancellationToken cancellationToken)
    {
        if (!await db.Cities.AnyAsync(c => c.Id == cityId && c.DeletedAt == null, cancellationToken))
        {
            throw BusinessException.Validation("Ville inconnue.", new { field = "cityId" });
        }
    }

    /// <summary>Villes couvertes en plus de la ville de l'agence, toutes connues.</summary>
    public static async Task<List<string>> CheckedCoverageAsync(IAppDbContext db, AgencyFields input, CancellationToken cancellationToken)
    {
        var wanted = input.CoveredCityIds!.Distinct().Where(id => id != input.CityId).ToList();
        if (wanted.Count == 0) return [];
        var found = await db.Cities.CountAsync(c => wanted.Contains(c.Id) && c.DeletedAt == null, cancellationToken);
        if (found != wanted.Count) throw BusinessException.Validation("Ville inconnue dans la couverture.", new { field = "coveredCityIds" });
        return wanted;
    }

    public static void Apply(Agency agency, AgencyFields input)
    {
        agency.Name = input.Name!;
        agency.CityId = input.CityId!;
        agency.Phone = Zod.EmptyToNull(input.Phone);
        agency.Email = Zod.EmptyToNull(input.Email);
        agency.Address = Zod.EmptyToNull(input.Address);
        agency.Currency = Zod.EmptyToNull(input.Currency);
        agency.Timezone = Zod.EmptyToNull(input.Timezone);
        agency.IsActive = input.IsActive ?? true;
    }
}
