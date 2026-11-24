using System.Globalization;
using System.Text.RegularExpressions;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Parcels;

/// <summary>`ParcelListQuery` une fois validé (`parcelListQuerySchema`).</summary>
public sealed record ParcelListFilter
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? Search { get; init; }
    public IReadOnlyList<int>? StatusId { get; init; }
    public IReadOnlyList<string>? StatusGroup { get; init; }
    public string? StatusIds { get; init; }
    public IReadOnlyList<string>? ClientId { get; init; }
    public IReadOnlyList<string>? CourierId { get; init; }
    public IReadOnlyList<string>? DestinationCityId { get; init; }
    public string? BillingState { get; init; }
    public IReadOnlyList<string>? FinancialStatus { get; init; }
    public IReadOnlyList<string>? MerchantFinancialStatus { get; init; }
    public IReadOnlyList<string>? Kind { get; init; }
    public string? NoteCandidate { get; init; }
    public string? Recent { get; init; }
    public string? Deleted { get; init; }
    public string? Period { get; init; }
    public string? From { get; init; }
    public string? To { get; init; }
}

/// <summary>Query string brute de la liste des colis : coercitions et messages zod.</summary>
public sealed class ParcelListParams
{
    private static readonly Regex Day = new(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled);

    public string? Page { get; set; }
    public string? PageSize { get; set; }
    public string? Search { get; set; }
    public string[]? StatusId { get; set; }
    public string[]? StatusGroup { get; set; }
    public string? StatusIds { get; set; }
    public string[]? ClientId { get; set; }
    public string[]? CourierId { get; set; }
    public string[]? DestinationCityId { get; set; }
    public string? BillingState { get; set; }
    public string[]? FinancialStatus { get; set; }
    public string[]? MerchantFinancialStatus { get; set; }
    public string[]? Kind { get; set; }
    public string? NoteCandidate { get; set; }
    public string? Recent { get; set; }
    public string? Deleted { get; set; }
    public string? Period { get; set; }
    public string? From { get; set; }
    public string? To { get; set; }

    public ParcelListFilter ToFilter(bool withPaging = true)
    {
        var issues = new List<object>();
        var paging = new ListQueryParams { Page = Page, PageSize = PageSize };
        int page = 1, pageSize = 10;
        if (withPaging)
        {
            var parsed = paging.ToQueryWithDefault(10);
            page = parsed.Page;
            pageSize = parsed.PageSize;
        }
        List<int>? statusIds = null;
        if (MultiValue.AsList(StatusId) is { } rawStatuses)
        {
            statusIds = [];
            for (var i = 0; i < rawStatuses.Count; i++)
            {
                if (double.TryParse(rawStatuses[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value == Math.Floor(value) && value > 0) statusIds.Add((int)value);
                else issues.Add(new { path = $"statusId.{i}", message = double.TryParse(rawStatuses[i], NumberStyles.Float, CultureInfo.InvariantCulture, out _) ? "Too small: expected number to be >0" : "Invalid input: expected number, received NaN" });
            }
            if (statusIds.Count > 200) issues.Add(new { path = "statusId", message = "Too big: expected array to have <=200 items" });
        }
        if (StatusIds is { Length: > 200 }) issues.Add(new { path = "statusIds", message = Common.Validation.Zod.TooBigString(200) });
        if (Period is not null && !Periods.IsPeriod(Period)) issues.Add(new { path = "period", message = Common.Validation.Zod.InvalidOption(Periods.All) });
        if (From is not null && !Day.IsMatch(From.Trim())) issues.Add(new { path = "from", message = "invalid-date" });
        if (To is not null && !Day.IsMatch(To.Trim())) issues.Add(new { path = "to", message = "invalid-date" });
        if (issues.Count > 0) throw BusinessException.Validation("Saisie invalide.", new { issues });
        return new ParcelListFilter
        {
            Page = page,
            PageSize = pageSize,
            Search = Search?.Trim(),
            StatusId = statusIds,
            StatusGroup = MultiValue.AsList(StatusGroup),
            StatusIds = StatusIds?.Trim(),
            ClientId = MultiValue.AsList(ClientId),
            CourierId = MultiValue.AsList(CourierId),
            DestinationCityId = MultiValue.AsList(DestinationCityId),
            BillingState = BillingState,
            FinancialStatus = MultiValue.AsList(FinancialStatus),
            MerchantFinancialStatus = MultiValue.AsList(MerchantFinancialStatus),
            Kind = MultiValue.AsList(Kind),
            NoteCandidate = NoteCandidate,
            Recent = Recent,
            Deleted = Deleted,
            Period = Period,
            From = From?.Trim(),
            To = To?.Trim(),
        };
    }
}

/// <summary>`scoped`, `where` et `whereInPeriod` de `ParcelsService`.</summary>
public static class ParcelFilters
{
    private static readonly DeliveryNoteStatus[] ActiveNoteStatuses = [DeliveryNoteStatus.AWAITING_VALIDATION, DeliveryNoteStatus.PICKED_UP_BY_COURIER];

    /// <summary>`scopeWhere` : le super-admin voit tout ; vendeur, livreur, agent leur part ; sinon rien.</summary>
    public static IQueryable<Parcel> Scoped(IQueryable<Parcel> parcels, ICurrentUser caller)
    {
        if (caller.IsSuperAdmin()) return parcels.Where(p => p.DeletedAt == null);
        if (caller.ClientId is { } clientId) return parcels.Where(p => p.ClientId == clientId && p.DeletedAt == null);
        if (caller.CourierId is { } courierId) return parcels.Where(p => p.CourierId == courierId && p.DeletedAt == null);
        if (caller.AgencyId is { } agencyId) return parcels.Where(p => p.AgencyId == agencyId && p.DeletedAt == null);
        return parcels.Where(p => p.Id == Scope.DenyAllId);
    }

    /// <summary>Comme <see cref="Scoped"/>, mais un super-admin peut demander les colis supprimés.</summary>
    public static IQueryable<Parcel> ScopedIncludingDeleted(IQueryable<Parcel> parcels, ICurrentUser caller)
    {
        if (caller.IsSuperAdmin()) return parcels;
        return Scoped(parcels, caller);
    }

    public static IQueryable<Parcel> Apply(IQueryable<Parcel> source, ICurrentUser caller, ParcelListFilter query)
    {
        var where = query.Deleted == "true" && caller.IsSuperAdmin()
            ? source.Where(p => p.DeletedAt != null)
            : Scoped(source, caller);
        if (query.StatusId is { Count: > 0 } statusIds) where = where.Where(p => statusIds.Contains(p.StatusId));
        else if (!string.IsNullOrEmpty(query.StatusIds))
        {
            var ids = query.StatusIds.Split(',').Select(v => int.TryParse(v.Trim(), out var id) ? id : 0).Where(id => id > 0).ToList();
            if (ids.Count > 0) where = where.Where(p => ids.Contains(p.StatusId));
        }
        if (query.ClientId is { Count: > 0 } clientIds && caller.MayFilterBy(caller.ClientId)) where = where.Where(p => clientIds.Contains(p.ClientId));
        if (query.CourierId is { Count: > 0 } courierIds && caller.MayFilterBy(caller.CourierId)) where = where.Where(p => p.CourierId != null && courierIds.Contains(p.CourierId));
        if (query.DestinationCityId is { Count: > 0 } cityIds) where = where.Where(p => cityIds.Contains(p.DestinationCityId));
        if (!string.IsNullOrEmpty(query.BillingState))
        {
            // Une valeur hors énumération fait échouer Prisma (500) : reproduit par Enum.Parse.
            var state = Enum.Parse<BillingState>(query.BillingState.ToUpperInvariant().Replace('-', '_'));
            where = where.Where(p => p.BillingState == state);
        }
        if (query.FinancialStatus is { Count: > 0 } financial && caller.CanSeeInternalCash())
        {
            var statuses = financial.Select(FinancialStatuses.FromWire).ToList();
            where = where.Where(p => statuses.Contains(p.FinancialStatus));
        }
        if (query.MerchantFinancialStatus is { Count: > 0 } merchant && !caller.CanSeeInternalCash())
        {
            var statuses = merchant.Where(m => FinancialStatuses.Merchant.Contains(m)).SelectMany(FinancialStatuses.InternalOf).Distinct().Select(FinancialStatuses.FromWire).ToList();
            where = where.Where(p => statuses.Contains(p.FinancialStatus));
        }
        if (query.Kind is { Count: > 0 } kinds)
        {
            var known = kinds.Select(ParcelViews.KindDb).OfType<ParcelKind>().ToList();
            if (known.Count > 0) where = where.Where(p => known.Contains(p.Kind));
        }
        if (query.StatusGroup is { Count: > 0 } groups)
        {
            var wanted = groups.Select(g => Enum.Parse<ParcelStatusGroup>(g.ToUpperInvariant())).ToList();
            where = where.Where(p => wanted.Contains(p.Status.Group));
        }
        if (query.NoteCandidate == "true") where = NoteCandidates(where);
        if (!string.IsNullOrWhiteSpace(query.Search)) where = Search(where, query.Search);
        if (caller.CourierId is not null) where = where.Where(p => p.StatusId != StatusCatalog.Delivered);
        return where;
    }

    /// <summary>`noteCandidateWhere` : en attente de ramassage ou d'arrivée, sur aucun bon actif.</summary>
    public static IQueryable<Parcel> NoteCandidates(IQueryable<Parcel> parcels) =>
        parcels.Where(p => (p.StatusId == StatusCatalog.AwaitingPickup || p.StatusId == StatusCatalog.AwaitingArrival)
                           && !p.NoteLinks.Any(l => ActiveNoteStatuses.Contains(l.DeliveryNote.Status)));

    /// <summary>`parcelSearchWhere`.</summary>
    public static IQueryable<Parcel> Search(IQueryable<Parcel> parcels, string search)
    {
        var needle = search.Trim();
        if (needle.Length == 0) return parcels;
        var pattern = Like.Contains(needle);
        return parcels.Where(p => EF.Functions.ILike(p.Code, pattern) || EF.Functions.ILike(p.ExternalCode!, pattern)
                                  || EF.Functions.ILike(p.RecipientName, pattern) || EF.Functions.ILike(p.RecipientPhone, pattern)
                                  || EF.Functions.ILike(p.Description, pattern) || EF.Functions.ILike(p.Client.Name, pattern)
                                  || EF.Functions.ILike(p.DestinationCity.Name, pattern) || EF.Functions.ILike(p.OriginCity.Name, pattern));
    }

    /// <summary>`whereInPeriod` : borne `createdAt` à la fenêtre de la période demandée (fuseau de la société).</summary>
    public static async Task<(IQueryable<Parcel> Where, PeriodWindow? Window)> InPeriodAsync(IAppDbContext db, ICurrentUser caller, ParcelListFilter query, IClock clock, CancellationToken cancellationToken)
    {
        var where = Apply(db.Parcels.AsNoTracking(), caller, query);
        if (query.Period is null) return (where, null);
        var timeZone = await db.Companies.AsNoTracking().OrderBy(c => c.CreatedAt).Select(c => c.Timezone).FirstOrDefaultAsync(cancellationToken);
        var first = await where.OrderBy(p => p.CreatedAt).Select(p => (DateTime?)p.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        var window = Periods.Resolve(query.Period, query.From, query.To, Periods.Zone(timeZone), first, clock.UtcNow);
        return (where.Where(p => p.CreatedAt >= window.Start && p.CreatedAt < window.End), window);
    }
}
