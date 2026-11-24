using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Idempotency;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.CourierApp;

/// <summary>`GET /courier/me/deliveries` : le menu Livraison, toutes tournées de livraison parties confondues.</summary>
public sealed record GetCourierDeliveriesQuery : IRequest<CourierDeliveriesDto>;

internal sealed class GetCourierDeliveriesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetCourierDeliveriesQuery, CourierDeliveriesDto>
{
    public async Task<CourierDeliveriesDto> Handle(GetCourierDeliveriesQuery request, CancellationToken cancellationToken)
    {
        var courierId = CourierRouteViews.CourierIdOf(caller);
        var rows = await CourierRouteViews.LoadAsync(db, db.Routes.AsNoTracking()
            .Where(r => r.CourierId == courierId && r.Kind == RouteKind.DELIVERY && (r.Status == RouteStatus.DISPATCHED || r.Status == RouteStatus.DELIVERING))
            .OrderByDescending(r => r.CreatedAt).Take(20), cancellationToken);
        var seen = new HashSet<string>();
        var parcels = new List<CourierDeliveryParcelDto>();
        var delivered = 0;
        foreach (var route in rows)
        {
            foreach (var link in route.Parcels)
            {
                if (!seen.Add(link.View.Id)) continue;
                if (link.View.StatusId == CourierPay.Delivered)
                {
                    delivered++;
                    continue;
                }
                if (!StatusCatalog.IsCourierDeliveryPending(link.View.StatusId)) continue;
                if (link.Handovers.Any(h => h.ReservedParcelId is not null && h.Status == ReturnHandoverStatus.AWAITING_RECEIPT)) continue;
                parcels.Add(Copy(link.View, route.Code));
            }
        }
        return new CourierDeliveriesDto(delivered, seen.Count, parcels);
    }

    private static CourierDeliveryParcelDto Copy(CourierRouteParcelDto p, string routeCode) => new()
    {
        Id = p.Id, Code = p.Code, ExternalCode = p.ExternalCode, Sequence = p.Sequence, RecipientName = p.RecipientName, RecipientPhone = p.RecipientPhone, DeliveryAddress = p.DeliveryAddress,
        DestinationCityName = p.DestinationCityName, DestinationDistrictName = p.DestinationDistrictName, SenderName = p.SenderName, SenderPhone = p.SenderPhone, PickupAddress = p.PickupAddress,
        OriginCityName = p.OriginCityName, StatusId = p.StatusId, CodToCollect = p.CodToCollect, Quantity = p.Quantity, CourierCommission = p.CourierCommission, Confirmed = p.Confirmed,
        Latitude = p.Latitude, Longitude = p.Longitude, RouteCode = routeCode,
    };
}
