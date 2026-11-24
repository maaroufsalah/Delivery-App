using System.Text.Json.Serialization;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Parcels;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Routes;

/// <summary>`RouteListItem`.</summary>
public class RouteListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StatusAt { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string CityId { get; set; } = string.Empty;
    public string OriginCityId { get; set; } = string.Empty;
    public string CourierId { get; set; } = string.Empty;
    public List<string> ParcelIds { get; set; } = [];
    public DateTime? ExitDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CourierName { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;
    public int ParcelCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int RemovedCount { get; set; }
}

/// <summary>`RouteParcel` : une ligne de la fiche du circuit.</summary>
public sealed record RouteParcelDto(string Id, string Code, string? ExternalCode, string RecipientName, string RecipientPhone, string DestinationCityName, string? DestinationDistrictName,
    string DeliveryAddress, string SenderName, string SenderPhone, string PickupAddress, string OriginCityName, int StatusId, long CodToCollect, bool Confirmed, long? CodCollected,
    string? Note, DateTime ParcelCreatedAt, int Quantity, DateTime? CorrectedAt, int? CorrectedStatusId);

/// <summary>`RouteNote` : un bon porté par le circuit.</summary>
public sealed record RouteNoteDto(string Id, string Code, string Status, string ClientName, string ClientPhone, string PickupAddress, string CityName, int ParcelCount, int RemovedCount,
    IReadOnlyList<string> ParcelIds, DateTime? PickedUpAt);

public sealed record PendingParcelDto(string Id, string Code, string? NoteCode);

/// <summary>`RouteDetail`.</summary>
public sealed class RouteDetailDto : RouteListItemDto
{
    [JsonPropertyOrder(1)] public List<RouteParcelDto> Parcels { get; set; } = [];
    [JsonPropertyOrder(1)] public List<RouteNoteDto> Notes { get; set; } = [];
    [JsonPropertyOrder(1)] public bool Closable { get; set; }
    [JsonPropertyOrder(1)] public List<PendingParcelDto> PendingParcels { get; set; } = [];
    [JsonPropertyOrder(1)] public int ClosurePicked { get; set; }
    [JsonPropertyOrder(1)] public int DeliveredCount { get; set; }
    [JsonPropertyOrder(1)] public int ToReturnCount { get; set; }
    [JsonPropertyOrder(1)] public int PendingHandoverCount { get; set; }
    [JsonPropertyOrder(1)] public bool Editable { get; set; }
    [JsonPropertyOrder(1)] public bool Dispatchable { get; set; }
    [JsonPropertyOrder(1)] public long CodCollectedTotal { get; set; }
}

public sealed record RouteStatsDto(int Total, int Open, int Parcels, int Confirmed);

/// <summary>`routeListQuerySchema` une fois lu.</summary>
public sealed record RouteListFilter(ListQuery Paging, IReadOnlyList<string>? Status, IReadOnlyList<string>? CityId, string? CourierId, string? Kind, string? Recent);

/// <summary>`closure-plan.ts` : ce qui empêche ou permet la clôture d'un circuit.</summary>
public static class ClosurePlans
{
    public sealed record Link(string Id, string Code, bool Confirmed, int StatusId);

    public sealed record Plan(int Picked, IReadOnlyList<Link> Unpicked, IReadOnlyList<Link> Blocking);

    public static Plan Of(RouteKind kind, IReadOnlyList<Link> links)
    {
        if (kind == RouteKind.PICKUP)
        {
            bool IsPicked(Link l) => l.StatusId is 18 or 3 || (l.Confirmed && l.StatusId == 17);
            bool IsUnpicked(Link l) => !l.Confirmed && l.StatusId is 1 or 17;
            bool IsReported(Link l) => l.Confirmed && !IsPicked(l);
            return new Plan(links.Count(IsPicked), links.Where(IsUnpicked).ToList(), links.Where(l => !IsPicked(l) && !IsUnpicked(l) && !IsReported(l)).ToList());
        }
        bool Blocks(Link l) => !l.Confirmed || (kind == RouteKind.DELIVERY && l.StatusId == 13);
        return new Plan(links.Count(l => !Blocks(l)), [], links.Where(Blocks).ToList());
    }

    public static bool IsClosable(RouteKind kind, RouteStatus status, Plan plan, int total)
    {
        if (status is RouteStatus.CLOSED or RouteStatus.DRAFT || total == 0) return false;
        if (plan.Blocking.Count > 0) return false;
        return kind != RouteKind.PICKUP || plan.Picked > 0;
    }
}

public static class RouteRows
{
    public const string Prefix = "CIR";

    /// <summary>`routeWhere` : le livreur ne voit que ses circuits.</summary>
    public static IQueryable<Route> Scoped(IQueryable<Route> routes, ICurrentUser caller) =>
        caller.CourierId is { } courierId ? routes.Where(r => r.CourierId == courierId) : routes;

    public static IQueryable<Route> Filter(IQueryable<Route> routes, ICurrentUser caller, RouteListFilter filter)
    {
        var where = Scoped(routes, caller);
        if (filter.Status is { Count: > 0 } statuses)
        {
            var wanted = statuses.Select(s => Enum.Parse<RouteStatus>(Money.FromWire(s))).ToList();
            where = where.Where(r => wanted.Contains(r.Status));
        }
        if (!string.IsNullOrEmpty(filter.Kind)) { var kind = Enum.Parse<RouteKind>(Money.FromWire(filter.Kind)); where = where.Where(r => r.Kind == kind); }
        if (filter.CityId is { Count: > 0 } cities) where = where.Where(r => cities.Contains(r.CityId));
        if (!string.IsNullOrEmpty(filter.CourierId) && caller.MayFilterBy(caller.CourierId)) where = where.Where(r => r.CourierId == filter.CourierId);
        if (!string.IsNullOrWhiteSpace(filter.Paging.Search))
        {
            var pattern = Like.Contains(filter.Paging.Search.Trim());
            where = where.Where(r => EF.Functions.ILike(r.Code, pattern) || EF.Functions.ILike(r.City.Name, pattern)
                                     || EF.Functions.ILike(r.Courier.LastName, pattern) || EF.Functions.ILike(r.Courier.FirstName, pattern));
        }
        return where;
    }

    internal sealed record Row(string Id, string Code, RouteStatus Status, DateTime StatusAt, RouteKind Kind, string CityId, string OriginCityId, string CourierId, DateTime? ExitDate,
        DateTime CreatedAt, string CourierFirst, string CourierLast, string CityName, int ParcelCount, int ConfirmedCount);

    internal static IQueryable<Row> Project(IQueryable<Route> routes) => routes.Select(r => new Row(r.Id, r.Code, r.Status, r.StatusAt, r.Kind, r.CityId, r.OriginCityId, r.CourierId,
        r.ExitDate, r.CreatedAt, r.Courier.FirstName, r.Courier.LastName, r.City.Name, r.Parcels.Count, r.Parcels.Count(p => p.Confirmed)));

    internal static T Fill<T>(T dto, Row r, int removed)
        where T : RouteListItemDto
    {
        dto.Id = r.Id;
        dto.Code = r.Code;
        dto.Status = Money.ToWire(r.Status);
        dto.StatusAt = r.StatusAt;
        dto.Kind = Money.ToWire(r.Kind);
        dto.CityId = r.CityId;
        dto.OriginCityId = r.OriginCityId;
        dto.CourierId = r.CourierId;
        dto.ExitDate = r.ExitDate;
        dto.CreatedAt = r.CreatedAt;
        dto.CourierName = $"{r.CourierFirst} {r.CourierLast}".Trim();
        dto.CityName = r.CityName;
        dto.ParcelCount = r.ParcelCount;
        dto.ConfirmedCount = r.ConfirmedCount;
        dto.RemovedCount = removed;
        return dto;
    }

    /// <summary>`removedCountsByRoute` : les colis retirés des bons de ces circuits.</summary>
    internal static async Task<Dictionary<string, int>> RemovedCountsAsync(IAppDbContext db, IReadOnlyList<string> routeIds, CancellationToken cancellationToken) =>
        routeIds.Count == 0 ? [] : (await db.DeliveryNoteRemovedParcels.AsNoTracking().Where(r => r.RouteId != null && routeIds.Contains(r.RouteId))
            .GroupBy(r => r.RouteId!).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(cancellationToken)).ToDictionary(g => g.Key, g => g.Count);

    public static async Task<Route> FindAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken, bool tracking = false)
    {
        var routes = tracking ? db.Routes : db.Routes.AsNoTracking();
        return await Scoped(routes, caller).FirstOrDefaultAsync(r => r.Code == code, cancellationToken) ?? throw BusinessException.NotFound($"Circuit introuvable : {code}");
    }

    public static async Task<RouteDetailDto> DetailAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken)
    {
        var row = await Project(Scoped(db.Routes.AsNoTracking(), caller).Where(r => r.Code == code)).FirstOrDefaultAsync(cancellationToken)
                  ?? throw BusinessException.NotFound($"Circuit introuvable : {code}");
        var lines = await db.RouteParcels.AsNoTracking().Where(l => l.RouteId == row.Id).OrderBy(l => l.Sequence).ThenBy(l => l.ParcelId)
            .Select(l => new
            {
                l.Parcel.Id, l.Parcel.Code, l.Parcel.ExternalCode, l.Parcel.RecipientName, l.Parcel.RecipientPhone, City = l.Parcel.DestinationCity.Name, l.Parcel.DestinationDistrictName,
                DistrictRef = l.Parcel.DestinationDistrict != null ? l.Parcel.DestinationDistrict.Name : null, l.Parcel.DeliveryAddress, Sender = l.Parcel.Client.Name, SenderPhone = l.Parcel.Client.Phone,
                l.Parcel.PickupAddress, Origin = l.Parcel.OriginCity.Name, l.Parcel.StatusId, l.Parcel.CodAmount, l.Parcel.DeliveryFee, l.Parcel.FeeIncluded, l.Parcel.AlreadyPaid, l.Confirmed,
                l.CodCollected, l.Note, l.Parcel.CreatedAt, l.Parcel.Quantity, l.CorrectedAt, l.CorrectedStatusId,
            }).ToListAsync(cancellationToken);
        var parcels = lines.Select(l => new RouteParcelDto(l.Id, l.Code, l.ExternalCode, l.RecipientName, l.RecipientPhone, l.City, ParcelViews.DistrictLabel(l.DestinationDistrictName, l.DistrictRef),
            l.DeliveryAddress, l.Sender, l.SenderPhone, l.PickupAddress, l.Origin, l.StatusId,
            Billing.CodToCollect(Money.ToCents(l.CodAmount), Money.ToCents(l.DeliveryFee), l.FeeIncluded, l.AlreadyPaid), l.Confirmed, Money.ToCentsOrNull(l.CodCollected), l.Note, l.CreatedAt,
            l.Quantity, l.CorrectedAt, l.CorrectedStatusId)).ToList();
        var notes = (await db.RouteNotes.AsNoTracking().Where(l => l.RouteId == row.Id).OrderBy(l => l.Sequence).ThenBy(l => l.DeliveryNoteId)
            .Select(l => new
            {
                l.DeliveryNote.Id, l.DeliveryNote.Code, l.DeliveryNote.Status, Client = l.DeliveryNote.Client.Name, l.DeliveryNote.Client.Phone, l.DeliveryNote.Client.Address,
                City = l.DeliveryNote.LocationCity.Name, ParcelIds = l.DeliveryNote.Parcels.Select(p => p.ParcelId).ToList(), Removed = l.DeliveryNote.RemovedParcels.Count, l.DeliveryNote.PickedUpAt,
            }).ToListAsync(cancellationToken))
            .Select(n => new RouteNoteDto(n.Id, n.Code, Money.ToWire(n.Status), n.Client, n.Phone, n.Address ?? string.Empty, n.City, n.ParcelIds.Count, n.Removed, n.ParcelIds, n.PickedUpAt)).ToList();
        var removed = await RemovedCountsAsync(db, [row.Id], cancellationToken);
        var plan = ClosurePlans.Of(row.Kind, parcels.Select(p => new ClosurePlans.Link(p.Id, p.Code, p.Confirmed, p.StatusId)).ToList());
        var noteOf = new Dictionary<string, string>();
        foreach (var note in notes) foreach (var id in note.ParcelIds) noteOf[id] = note.Code;
        var open = row.Status is not (RouteStatus.CLOSED or RouteStatus.DRAFT);
        var completion = await DeliveryCompletionAsync(db, parcels.Select(p => (p.Id, p.StatusId)).ToList(), cancellationToken);
        var detail = Fill(new RouteDetailDto(), row, removed.GetValueOrDefault(row.Id));
        detail.Parcels = parcels;
        detail.Notes = notes;
        detail.Closable = row.Kind != RouteKind.DELIVERY && ClosurePlans.IsClosable(row.Kind, row.Status, plan, parcels.Count);
        detail.PendingParcels = open ? plan.Unpicked.Concat(plan.Blocking).Select(l => new PendingParcelDto(l.Id, l.Code, noteOf.GetValueOrDefault(l.Id))).ToList() : [];
        detail.ClosurePicked = plan.Picked;
        detail.DeliveredCount = completion.Delivered;
        detail.ToReturnCount = completion.ToReturn;
        detail.PendingHandoverCount = completion.PendingHandovers;
        detail.Editable = row.Status == RouteStatus.DRAFT;
        detail.Dispatchable = row.Status == RouteStatus.DRAFT && parcels.Count > 0;
        detail.CodCollectedTotal = parcels.Sum(p => p.CodCollected ?? 0);
        return detail;
    }

    private static readonly ReturnHandoverStatus[] HandoverLive = [ReturnHandoverStatus.RETURN_REQUESTED, ReturnHandoverStatus.PICKED_UP, ReturnHandoverStatus.AWAITING_RECEIPT,
        ReturnHandoverStatus.CONFIRMED, ReturnHandoverStatus.CLOSED];

    /// <summary>`deliveryCompletion` : livrés, retours reçus, à retourner, remises de retour en cours.</summary>
    internal static async Task<(int Delivered, int Returned, int ToReturn, int PendingHandovers)> DeliveryCompletionAsync(IAppDbContext db, IReadOnlyList<(string Id, int StatusId)> parcels,
        CancellationToken cancellationToken)
    {
        var ids = parcels.Select(p => p.Id).ToList();
        var items = ids.Count == 0 ? [] : await db.ReturnHandoverItems.AsNoTracking().Where(i => ids.Contains(i.ParcelId) && i.CorrectedAt == null && HandoverLive.Contains(i.Handover.Status))
            .Select(i => new { i.ParcelId, i.HandoverId, i.Handover.Status }).ToListAsync(cancellationToken);
        var received = items.Where(i => i.Status is ReturnHandoverStatus.CONFIRMED or ReturnHandoverStatus.CLOSED).Select(i => i.ParcelId).ToHashSet();
        var pending = items.Where(i => i.Status is not (ReturnHandoverStatus.CONFIRMED or ReturnHandoverStatus.CLOSED)).Select(i => i.HandoverId).Distinct().Count();
        var delivered = parcels.Count(p => p.StatusId == 4);
        var returned = parcels.Count(p => p.StatusId != 4 && p.StatusId != 13 && received.Contains(p.Id));
        return (delivered, returned, parcels.Count - delivered - returned, pending);
    }
}
