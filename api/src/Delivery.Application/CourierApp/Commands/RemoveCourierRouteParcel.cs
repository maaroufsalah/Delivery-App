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

/// <summary>`DELETE /courier/me/routes/:code/parcels/:parcelId` : le livreur retire un colis de son brouillon.</summary>
public sealed record RemoveCourierRouteParcelCommand(string Code, string ParcelId) : IRequest<CourierRouteDto>;

internal sealed class RemoveCourierRouteParcelHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RemoveCourierRouteParcelCommand, CourierRouteDto>
{
    public async Task<CourierRouteDto> Handle(RemoveCourierRouteParcelCommand request, CancellationToken cancellationToken)
    {
        var courierId = CourierRouteViews.CourierIdOf(caller);
        var route = await db.Routes.AsNoTracking().FirstOrDefaultAsync(r => r.CourierId == courierId && r.Code == request.Code, cancellationToken)
                    ?? throw BusinessException.NotFound($"Tournée introuvable : {request.Code}");
        if (route.Status != RouteStatus.DRAFT)
        {
            throw BusinessException.Conflict("Cette tournée est partie : elle ne se recompose plus depuis le téléphone.", new { reason = "route-dispatched" });
        }
        await RouteWrites.RemoveParcelFromDraftAsync(db, caller, route, request.ParcelId, "courier", cancellationToken);
        return await CourierRouteViews.RouteAsync(db, caller, request.Code, cancellationToken);
    }
}
