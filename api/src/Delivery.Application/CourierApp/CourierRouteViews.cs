using Delivery.Application.Common.Interfaces;
using Delivery.Application.Parcels;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.CourierApp;

/// <summary>`CourierRouteParcel` : un colis de la tournée, vu du téléphone.</summary>
public class CourierRouteParcelDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? ExternalCode { get; set; }
    public int Sequence { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientPhone { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public string DestinationCityName { get; set; } = string.Empty;
    public string? DestinationDistrictName { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderPhone { get; set; } = string.Empty;
    public string PickupAddress { get; set; } = string.Empty;
    public string OriginCityName { get; set; } = string.Empty;
    public int StatusId { get; set; }
    public long CodToCollect { get; set; }
    public int Quantity { get; set; }
    public long? CourierCommission { get; set; }
    public bool Confirmed { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

/// <summary>`CourierDeliveryParcel` : le même, avec la tournée qui le porte.</summary>
public sealed class CourierDeliveryParcelDto : CourierRouteParcelDto
{
    public string RouteCode { get; set; } = string.Empty;
}

public sealed record CourierRouteNoteDto(string Id, string Code, string Status, string ClientName, string ClientPhone, string PickupAddress, string CityName, int ParcelCount, int RemovedCount,
    IReadOnlyList<string> ParcelIds, DateTime? PickedUpAt);

public sealed record CourierRouteDto(string Id, string Code, string Kind, string Status, string CityId, string CityName, DateTime CreatedAt, int ParcelCount, int RemainingCount, int DeliveredCount,
    int ToReturnCount, int PendingHandoverCount, long CodToCollect, IReadOnlyList<CourierRouteParcelDto> Parcels, IReadOnlyList<CourierRouteNoteDto> Notes);

public sealed record CourierDeliveriesDto(int DeliveredCount, int TotalCount, IReadOnlyList<CourierDeliveryParcelDto> Parcels);

/// <summary>`CourierService` : les tournées du livreur connecté (`routeInclude` + `toRoute`).</summary>
public static class CourierRouteViews
{
    public sealed record HandoverRef(string HandoverId, string? ReservedParcelId, ReturnHandoverStatus Status);

    public sealed record ParcelRow(CourierRouteParcelDto View, IReadOnlyList<HandoverRef> Handovers);

    public sealed record RouteRow(string Id, string Code, RouteKind Kind, RouteStatus Status, string CityId, string CityName, DateTime CreatedAt, IReadOnlyList<ParcelRow> Parcels,
        IReadOnlyList<CourierRouteNoteDto> Notes);

    public static string CourierIdOf(ICurrentUser caller) =>
        caller.CourierId ?? throw BusinessException.Forbidden("Cet espace est réservé aux livreurs.", new { reason = "not-a-courier" });

    public static async Task<List<RouteRow>> LoadAsync(IAppDbContext db, IQueryable<Route> routes, CancellationToken cancellationToken)
    {
        var rows = await routes.Select(r => new
        {
            r.Id, r.Code, r.Kind, r.Status, r.CityId, City = r.City.Name, r.CreatedAt,
            Parcels = r.Parcels.OrderBy(l => l.Sequence).ThenBy(l => l.ParcelId).Select(l => new
            {
                l.Sequence, l.Confirmed, l.CourierCommission, l.Parcel.Id, l.Parcel.Code, l.Parcel.ExternalCode, l.Parcel.RecipientName, l.Parcel.RecipientPhone, l.Parcel.DeliveryAddress,
                l.Parcel.StatusId, l.Parcel.CodAmount, l.Parcel.DeliveryFee, l.Parcel.FeeIncluded, l.Parcel.Quantity, l.Parcel.AlreadyPaid, l.Parcel.Latitude, l.Parcel.Longitude,
                l.Parcel.PickupAddress, Sender = l.Parcel.Client.Name, SenderPhone = l.Parcel.Client.Phone, Origin = l.Parcel.OriginCity.Name, Destination = l.Parcel.DestinationCity.Name,
                l.Parcel.DestinationDistrictName, DistrictRef = l.Parcel.DestinationDistrict != null ? l.Parcel.DestinationDistrict.Name : null,
                Handovers = l.Parcel.HandoverItems.Select(i => new HandoverRef(i.HandoverId, i.ReservedParcelId, i.Handover.Status)).ToList(),
            }).ToList(),
            Notes = r.Notes.OrderBy(n => n.Sequence).ThenBy(n => n.DeliveryNoteId).Select(n => new
            {
                n.DeliveryNote.Id, n.DeliveryNote.Code, n.DeliveryNote.Status, Client = n.DeliveryNote.Client.Name, Phone = n.DeliveryNote.Client.Phone, n.DeliveryNote.Client.Address,
                City = n.DeliveryNote.LocationCity.Name, ParcelIds = n.DeliveryNote.Parcels.Select(p => p.ParcelId).ToList(), Removed = n.DeliveryNote.RemovedParcels.Count, n.DeliveryNote.PickedUpAt,
            }).ToList(),
        }).ToListAsync(cancellationToken);
        return rows.Select(r => new RouteRow(r.Id, r.Code, r.Kind, r.Status, r.CityId, r.City, r.CreatedAt,
            r.Parcels.Select(p => new ParcelRow(new CourierRouteParcelDto
            {
                Id = p.Id, Code = p.Code, ExternalCode = p.ExternalCode, Sequence = p.Sequence, RecipientName = p.RecipientName, RecipientPhone = p.RecipientPhone, DeliveryAddress = p.DeliveryAddress,
                DestinationCityName = p.Destination, DestinationDistrictName = ParcelViews.DistrictLabel(p.DestinationDistrictName, p.DistrictRef), SenderName = p.Sender, SenderPhone = p.SenderPhone,
                PickupAddress = p.PickupAddress, OriginCityName = p.Origin, StatusId = p.StatusId,
                CodToCollect = Billing.CodToCollect(Money.ToCents(p.CodAmount), Money.ToCents(p.DeliveryFee), p.FeeIncluded, p.AlreadyPaid), Quantity = p.Quantity,
                CourierCommission = Money.ToCentsOrNull(p.CourierCommission), Confirmed = p.Confirmed, Latitude = p.Latitude is { } lat ? (double)lat : null,
                Longitude = p.Longitude is { } lng ? (double)lng : null,
            }, p.Handovers)).ToList(),
            r.Notes.Select(n => new CourierRouteNoteDto(n.Id, n.Code, Money.ToWire(n.Status), n.Client, n.Phone, n.Address ?? string.Empty, n.City, n.ParcelIds.Count, n.Removed, n.ParcelIds,
                n.PickedUpAt)).ToList())).ToList();
    }

    public static CourierRouteDto ToRoute(RouteRow row)
    {
        var parcels = row.Parcels.Select(p => p.View).ToList();
        var (delivered, toReturn, pending) = CompletionOf(row.Parcels);
        return new CourierRouteDto(row.Id, row.Code, Money.ToWire(row.Kind), row.Status == RouteStatus.DRAFT ? "draft" : row.Status == RouteStatus.CLOSED ? "closed" : "active", row.CityId,
            row.CityName, row.CreatedAt, parcels.Count, parcels.Count(p => !p.Confirmed), delivered, toReturn, pending, parcels.Where(p => !p.Confirmed).Sum(p => p.CodToCollect), parcels, row.Notes);
    }

    /// <summary>`completionOf` : livrés, à rendre à l'agence, bons de retour en cours.</summary>
    private static (int Delivered, int ToReturn, int PendingHandovers) CompletionOf(IReadOnlyList<ParcelRow> links)
    {
        static bool Received(ReturnHandoverStatus s) => s is ReturnHandoverStatus.CONFIRMED or ReturnHandoverStatus.CLOSED;
        var pending = new HashSet<string>();
        int delivered = 0, returned = 0;
        foreach (var link in links)
        {
            if (link.View.StatusId == CourierPay.Delivered)
            {
                delivered++;
                continue;
            }
            if (link.Handovers.Any(h => Received(h.Status))) returned++;
            foreach (var item in link.Handovers.Where(h => !Received(h.Status) && h.Status != ReturnHandoverStatus.CANCELLED)) pending.Add(item.HandoverId);
        }
        return (delivered, links.Count - delivered - returned, pending.Count);
    }

    /// <summary>`route` : une tournée du livreur (les clôturées seulement sur demande).</summary>
    public static async Task<CourierRouteDto> RouteAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken, bool includeClosed = false)
    {
        var courierId = CourierIdOf(caller);
        var query = db.Routes.AsNoTracking().Where(r => r.CourierId == courierId && r.Code == code);
        if (!includeClosed) query = query.Where(r => r.Status != RouteStatus.CLOSED);
        var row = (await LoadAsync(db, query, cancellationToken)).FirstOrDefault() ?? throw BusinessException.NotFound($"Tournée introuvable : {code}");
        return ToRoute(row);
    }
}
