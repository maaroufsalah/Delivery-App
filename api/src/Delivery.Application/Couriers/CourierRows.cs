using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Couriers;

/// <summary>`CourierListItem` : énumérations au format « fil ».</summary>
public class CourierListItemDto
{
    public string Id { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string CityId { get; set; } = string.Empty;

    public string CityName { get; set; } = string.Empty;

    public string? AgencyId { get; set; }

    public string? AgencyName { get; set; }

    public string VehicleType { get; set; } = string.Empty;

    public string PayModel { get; set; } = string.Empty;

    public string SettlementMode { get; set; } = string.Empty;

    public string PayPeriod { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime? ValidatedAt { get; set; }

    public string? ValidatedByName { get; set; }

    public DateTime? RejectedAt { get; set; }

    public string? RejectedByName { get; set; }

    public string? RejectionReason { get; set; }

    public string? UserId { get; set; }

    public int ParcelCount { get; set; }
}

public sealed record CourierCityRateDto(string CityId, string CityName, long PickupFee, long DeliveryFee);

/// <summary>`CourierDetail` : la fiche et sa grille de paie par ville (centimes).</summary>
public sealed class CourierDetailDto : CourierListItemDto
{
    [JsonPropertyOrder(1)]
    public string? NationalId { get; set; }

    [JsonPropertyOrder(1)]
    public string? BankName { get; set; }

    [JsonPropertyOrder(1)]
    public string? Rib { get; set; }

    [JsonPropertyOrder(1)]
    public long? FixedSalary { get; set; }

    [JsonPropertyOrder(1)]
    public IReadOnlyList<CourierCityRateDto> Cities { get; set; } = [];
}

public sealed class CouriersProfile : Profile
{
    public CouriersProfile()
    {
        CreateMap<Courier, CourierListItemDto>()
            .ForMember(d => d.CityName, o => o.MapFrom(s => s.City.Name))
            .ForMember(d => d.AgencyName, o => o.MapFrom(s => s.Agency != null ? s.Agency.Name : null))
            .ForMember(d => d.VehicleType, o => o.MapFrom(s => s.VehicleType.ToString()))
            .ForMember(d => d.PayModel, o => o.MapFrom(s => s.PayModel.ToString()))
            .ForMember(d => d.SettlementMode, o => o.MapFrom(s => s.SettlementMode.ToString()))
            .ForMember(d => d.PayPeriod, o => o.MapFrom(s => s.PayPeriod.ToString()))
            .ForMember(d => d.ValidatedByName, o => o.MapFrom(s => s.ValidatedBy != null ? s.ValidatedBy.FirstName + " " + s.ValidatedBy.LastName : null))
            .ForMember(d => d.RejectedByName, o => o.MapFrom(s => s.RejectedBy != null ? s.RejectedBy.FirstName + " " + s.RejectedBy.LastName : null))
            .ForMember(d => d.ParcelCount, o => o.MapFrom(s => s.Parcels.Count));
        CreateMap<Courier, CourierDetailDto>()
            .IncludeBase<Courier, CourierListItemDto>()
            .ForMember(d => d.FixedSalary, o => o.Ignore())
            .ForMember(d => d.Cities, o => o.Ignore());
    }
}

/// <summary>`courierSchema`.</summary>
public abstract record CourierFields
{
    public static readonly string[] VehicleTypes = ["motorbike", "car", "van"];
    public static readonly string[] PayModels = ["salary", "commission", "hybrid"];
    public static readonly string[] SettlementModes = ["at-remittance", "monthly"];
    public static readonly string[] PayPeriods = ["daily", "weekly", "monthly"];

    public string? FirstName { get; init => field = Zod.Trim(value); }

    public string? LastName { get; init => field = Zod.Trim(value); }

    public string? Phone { get; init => field = value is null ? null : Phones.Normalize(value); }

    public string? NationalId { get; init => field = Zod.Trim(value); }

    public string? CityId { get; init; }

    public string? AgencyId { get; init; }

    public string? VehicleType { get; init; } = "motorbike";

    public string? BankName { get; init => field = Zod.Trim(value); }

    public string? Rib { get; init => field = Zod.Trim(value); }

    public string? PayModel { get; init; } = "commission";

    public string? SettlementMode { get; init; } = "at-remittance";

    public string? PayPeriod { get; init; } = "monthly";

    public double? FixedSalary { get; init; }

    public bool? IsActive { get; init; } = true;
}

public abstract class CourierFieldsValidator<T> : ZodValidator<T>
    where T : CourierFields
{
    private static readonly Regex RibPattern = new(@"^\d{24}$", RegexOptions.Compiled);

    protected CourierFieldsValidator()
    {
        RuleFor(x => x.FirstName).Required().MinLength(2, "too-short").MaxLength(80);
        RuleFor(x => x.LastName).Required().MinLength(2, "too-short").MaxLength(80);
        RuleFor(x => x.Phone).Required().Pattern(Phones.Pattern, "invalid-phone");
        RuleFor(x => x.NationalId).Must(v => v is null || v.Length <= 30).WithMessage(Zod.InvalidInput);
        RuleFor(x => x.CityId).Required().MinLength(1, "required");
        RuleFor(x => x.VehicleType).OneOf(CourierFields.VehicleTypes);
        RuleFor(x => x.BankName).Must(v => v is null || v.Length <= 80).WithMessage(Zod.InvalidInput);
        RuleFor(x => x.Rib).Must(v => string.IsNullOrEmpty(v) || RibPattern.IsMatch(v)).WithMessage(Zod.InvalidInput);
        RuleFor(x => x.PayModel).OneOf(CourierFields.PayModels);
        RuleFor(x => x.SettlementMode).OneOf(CourierFields.SettlementModes);
        RuleFor(x => x.PayPeriod).OneOf(CourierFields.PayPeriods);
        RuleFor(x => x.FixedSalary).MadAmount(nullable: true);
    }
}

internal static class CourierRows
{
    public static readonly IReadOnlyDictionary<string, Func<IQueryable<Courier>, bool, IOrderedQueryable<Courier>>> Sorts =
        new Dictionary<string, Func<IQueryable<Courier>, bool, IOrderedQueryable<Courier>>>
        {
            ["lastName"] = (q, d) => d ? q.OrderByDescending(c => c.LastName) : q.OrderBy(c => c.LastName),
            ["cityName"] = (q, d) => d ? q.OrderByDescending(c => c.City.Name) : q.OrderBy(c => c.City.Name),
            ["agencyName"] = (q, d) => d ? q.OrderByDescending(c => c.Agency!.Name) : q.OrderBy(c => c.Agency!.Name),
            ["isActive"] = (q, d) => d ? q.OrderByDescending(c => c.IsActive) : q.OrderBy(c => c.IsActive),
            ["createdAt"] = (q, d) => d ? q.OrderByDescending(c => c.CreatedAt) : q.OrderBy(c => c.CreatedAt),
        };

    public static async Task<List<T>> ProjectAsync<T>(IQueryable<Courier> couriers, IMapper mapper, CancellationToken cancellationToken)
        where T : CourierListItemDto
    {
        var rows = await couriers.ProjectTo<T>(mapper.ConfigurationProvider).ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            row.VehicleType = Money.ToWire(row.VehicleType);
            row.PayModel = Money.ToWire(row.PayModel);
            row.SettlementMode = Money.ToWire(row.SettlementMode);
            row.PayPeriod = Money.ToWire(row.PayPeriod);
        }
        return rows;
    }

    public static async Task<CourierDetailDto> DetailAsync(IAppDbContext db, IMapper mapper, string id, CancellationToken cancellationToken)
    {
        var query = db.Couriers.AsNoTracking().Where(c => c.Id == id && c.DeletedAt == null);
        var detail = (await ProjectAsync<CourierDetailDto>(query, mapper, cancellationToken)).FirstOrDefault()
            ?? throw BusinessException.NotFound("Livreur introuvable.");
        detail.FixedSalary = Money.ToCentsOrNull(await query.Select(c => c.FixedSalary).FirstAsync(cancellationToken));
        var cities = await db.CourierCities.AsNoTracking().Where(c => c.CourierId == id).OrderBy(c => c.CityId)
            .Select(c => new { c.CityId, c.City.Name, c.PickupFee, c.DeliveryFee }).ToListAsync(cancellationToken);
        detail.Cities = cities.Select(c => new CourierCityRateDto(c.CityId, c.Name, Money.ToCents(c.PickupFee), Money.ToCents(c.DeliveryFee))).ToList();
        return detail;
    }

    public static async Task<Courier> FindAsync(IAppDbContext db, string id, CancellationToken cancellationToken) =>
        await db.Couriers.FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null, cancellationToken)
        ?? throw BusinessException.NotFound("Livreur introuvable.");

    public static void Apply(Courier courier, CourierFields input, string? fallbackAgencyId)
    {
        courier.FirstName = input.FirstName!;
        courier.LastName = input.LastName!;
        courier.Phone = input.Phone!;
        courier.NationalId = Zod.EmptyToNull(input.NationalId);
        courier.CityId = input.CityId!;
        courier.AgencyId = Zod.EmptyToNull(input.AgencyId) ?? fallbackAgencyId;
        courier.VehicleType = Enum.Parse<Domain.Enums.VehicleType>(Money.FromWire(input.VehicleType ?? "motorbike"));
        courier.BankName = Zod.EmptyToNull(input.BankName);
        courier.Rib = Zod.EmptyToNull(input.Rib);
        courier.PayModel = Enum.Parse<Domain.Enums.PayModel>(Money.FromWire(input.PayModel ?? "commission"));
        courier.SettlementMode = Enum.Parse<Domain.Enums.SettlementMode>(Money.FromWire(input.SettlementMode ?? "at-remittance"));
        courier.PayPeriod = Enum.Parse<Domain.Enums.PayPeriod>(Money.FromWire(input.PayPeriod ?? "monthly"));
        courier.FixedSalary = input.FixedSalary is null ? null : Money.FromMad(input.FixedSalary.Value);
        courier.IsActive = input.IsActive ?? true;
    }
}
