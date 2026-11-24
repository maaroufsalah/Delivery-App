using AutoMapper;
using AutoMapper.QueryableExtensions;
using Delivery.Application.Clients.Dtos;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Clients;

public sealed class ClientsProfile : Profile
{
    public ClientsProfile()
    {
        CreateMap<Client, ClientListItemDto>()
            .ForMember(d => d.Type, o => o.MapFrom(s => s.Type.ToString()))
            .ForMember(d => d.BillingType, o => o.MapFrom(s => s.BillingType.ToString()))
            .ForMember(d => d.CityName, o => o.MapFrom(s => s.City.Name))
            .ForMember(d => d.AgencyName, o => o.MapFrom(s => s.Agency != null ? s.Agency.Name : null))
            .ForMember(d => d.PickupFee, o => o.Ignore())
            .ForMember(d => d.ValidatedByName, o => o.MapFrom(s => s.ValidatedBy != null ? s.ValidatedBy.FirstName + " " + s.ValidatedBy.LastName : null))
            .ForMember(d => d.RejectedByName, o => o.MapFrom(s => s.RejectedBy != null ? s.RejectedBy.FirstName + " " + s.RejectedBy.LastName : null))
            .ForMember(d => d.ParcelCount, o => o.MapFrom(s => s.Parcels.Count));
        CreateMap<Client, ClientDetailDto>()
            .IncludeBase<Client, ClientListItemDto>()
            .ForMember(d => d.Discounts, o => o.Ignore())
            .ForMember(d => d.PickupAddresses, o => o.Ignore())
            .ForMember(d => d.Documents, o => o.Ignore());
    }
}

/// <summary>Lecture partagée des fiches clients : portée, projection, conversions « fil ».</summary>
public static class ClientRows
{
    public static readonly IReadOnlyDictionary<string, Func<IQueryable<Client>, bool, IOrderedQueryable<Client>>> Sorts =
        new Dictionary<string, Func<IQueryable<Client>, bool, IOrderedQueryable<Client>>>
        {
            ["name"] = (q, d) => d ? q.OrderByDescending(c => c.Name) : q.OrderBy(c => c.Name),
            ["cityName"] = (q, d) => d ? q.OrderByDescending(c => c.City.Name) : q.OrderBy(c => c.City.Name),
            ["agencyName"] = (q, d) => d ? q.OrderByDescending(c => c.Agency!.Name) : q.OrderBy(c => c.Agency!.Name),
            ["createdAt"] = (q, d) => d ? q.OrderByDescending(c => c.CreatedAt) : q.OrderBy(c => c.CreatedAt),
            ["isActive"] = (q, d) => d ? q.OrderByDescending(c => c.IsActive) : q.OrderBy(c => c.IsActive),
        };

    /// <summary>`scopeFilter` : le vendeur se voit seul, l'agent voit son agence.</summary>
    public static IQueryable<Client> Scoped(IQueryable<Client> clients, ICurrentUser caller)
    {
        if (caller.IsSuperAdmin()) return clients;
        if (caller.ClientId is { } clientId) return clients.Where(c => c.Id == clientId);
        if (caller.AgencyId is { } agencyId) return clients.Where(c => c.AgencyId == agencyId);
        return clients;
    }

    /// <summary>Projection puis conversions : énumérations en minuscules à tirets, frais de ramassage en centimes.</summary>
    public static async Task<List<T>> ProjectAsync<T>(IQueryable<Client> clients, IMapper mapper, CancellationToken cancellationToken)
        where T : ClientListItemDto
    {
        var rows = await clients.ProjectTo<T>(mapper.ConfigurationProvider).ToListAsync(cancellationToken);
        var ids = rows.Select(r => r.Id).ToList();
        var fees = await clients.Where(c => ids.Contains(c.Id)).Select(c => new { c.Id, c.PickupFee }).ToDictionaryAsync(c => c.Id, c => c.PickupFee, cancellationToken);
        foreach (var row in rows)
        {
            row.Type = Money.ToWire(row.Type);
            row.BillingType = Money.ToWire(row.BillingType);
            row.PickupFee = Money.ToCentsOrNull(fees.GetValueOrDefault(row.Id));
        }
        return rows;
    }

    public static async Task<ClientDetailDto> DetailAsync(IAppDbContext db, ICurrentUser caller, IMapper mapper, string id, CancellationToken cancellationToken)
    {
        var query = Scoped(db.Clients.AsNoTracking().Where(c => c.Id == id && c.DeletedAt == null), caller);
        var detail = (await ProjectAsync<ClientDetailDto>(query, mapper, cancellationToken)).FirstOrDefault()
            ?? throw BusinessException.NotFound("Client introuvable.");
        var extra = await db.Clients.AsNoTracking().Where(c => c.Id == id).Select(c => new { c.Address, c.Notes }).FirstAsync(cancellationToken);
        detail.Address = extra.Address;
        detail.Notes = extra.Notes;

        var discounts = await db.ClientDiscounts.AsNoTracking().Where(d => d.ClientId == id).OrderBy(d => d.CreatedAt).ThenBy(d => d.Id)
            .Select(d => new { d.Id, d.ZoneIds, Cities = d.Cities.Select(c => c.CityId).ToList(), d.FlatRate, d.SameCityRate, d.PickupFee, d.IsActive })
            .ToListAsync(cancellationToken);
        detail.Discounts = discounts
            .Select(d => new ClientDiscountItemDto(d.Id, d.ZoneIds, d.Cities, Money.ToCentsOrNull(d.FlatRate), Money.ToCentsOrNull(d.SameCityRate), Money.ToCentsOrNull(d.PickupFee), d.IsActive))
            .OrderBy(d => TargetRank(d.ZoneIds, d.CityIds))
            .ToList();
        detail.PickupAddresses = await db.ClientPickupAddresses.AsNoTracking().Where(a => a.ClientId == id && a.DeletedAt == null).OrderBy(a => a.CreatedAt)
            .Select(a => new ClientPickupAddressItemDto(a.Id, a.Label, a.Address, a.CityId, a.City.Name, a.DistrictId, a.District != null ? a.District.Name : null, a.Phone, a.IsDefault))
            .ToListAsync(cancellationToken);
        var documents = await db.ClientDocuments.AsNoTracking().Where(d => d.ClientId == id && d.DeletedAt == null).OrderByDescending(d => d.CreatedAt)
            .Select(d => new { d.Id, d.Name, d.Kind, d.MimeType, d.SizeBytes, d.CreatedAt, By = d.UploadedBy != null ? d.UploadedBy.FirstName + " " + d.UploadedBy.LastName : null })
            .ToListAsync(cancellationToken);
        detail.Documents = documents.Select(d => new ClientDocumentItemDto(d.Id, d.Name, Money.ToWire(d.Kind), d.MimeType, d.SizeBytes, d.CreatedAt, d.By)).ToList();
        return detail;
    }

    /// <summary>`discountTarget` rangé : ville d'abord, puis zone, puis « toutes ».</summary>
    public static int TargetRank(IReadOnlyCollection<string> zoneIds, IReadOnlyCollection<string> cityIds) => cityIds.Count > 0 ? 0 : zoneIds.Count > 0 ? 1 : 2;

    public static async Task<Client> FindAsync(IAppDbContext db, string id, CancellationToken cancellationToken) =>
        await db.Clients.FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null, cancellationToken)
        ?? throw BusinessException.NotFound("Client introuvable.");

    public static async Task AssertCityExistsAsync(IAppDbContext db, string cityId, CancellationToken cancellationToken)
    {
        if (!await db.Cities.AnyAsync(c => c.Id == cityId && c.DeletedAt == null, cancellationToken)) throw BusinessException.Validation("Ville inconnue.", new { field = "cityId" });
    }

    public static async Task AssertAgencyExistsAsync(IAppDbContext db, string agencyId, CancellationToken cancellationToken)
    {
        if (!await db.Agencies.AnyAsync(a => a.Id == agencyId && a.DeletedAt == null, cancellationToken)) throw BusinessException.Validation("Agence inconnue.", new { field = "agencyId" });
    }

    /// <summary>`formatAmount` des PDF : `12,50 MAD`.</summary>
    public static string FormatAmount(long cents)
    {
        var sign = cents < 0 ? "-" : string.Empty;
        var absolute = Math.Abs(cents);
        return $"{sign}{absolute / 100},{absolute % 100:00} MAD";
    }
}
