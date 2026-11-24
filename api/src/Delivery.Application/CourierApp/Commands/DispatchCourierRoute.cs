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

/// <summary>`POST /courier/me/routes/:code/dispatch` : le livreur fait partir sa tournée.</summary>
public sealed record DispatchCourierRouteCommand(string Code) : IRequest<CourierRouteDto>;

internal sealed class DispatchCourierRouteHandler(IAppDbContext db, ICurrentUser caller, ILogger<DispatchCourierRouteHandler> logger) : IRequestHandler<DispatchCourierRouteCommand, CourierRouteDto>
{
    public async Task<CourierRouteDto> Handle(DispatchCourierRouteCommand request, CancellationToken cancellationToken)
    {
        var courierId = CourierRouteViews.CourierIdOf(caller);
        if (!await db.Routes.AnyAsync(r => r.CourierId == courierId && r.Code == request.Code, cancellationToken)) throw BusinessException.NotFound($"Tournée introuvable : {request.Code}");
        await BulkJobs.BulkJobWrites.AssertIdleAsync(db, request.Code, cancellationToken);
        await RouteDispatch.RunAsync(db, caller, logger, request.Code, new RouteDispatch.Options(), cancellationToken);
        return await CourierRouteViews.RouteAsync(db, caller, request.Code, cancellationToken);
    }
}
