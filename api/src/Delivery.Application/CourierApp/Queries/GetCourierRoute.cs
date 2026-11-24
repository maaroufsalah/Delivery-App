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

/// <summary>`GET /courier/me/routes/:code`.</summary>
public sealed record GetCourierRouteQuery(string Code) : IRequest<CourierRouteDto>;

internal sealed class GetCourierRouteHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetCourierRouteQuery, CourierRouteDto>
{
    public Task<CourierRouteDto> Handle(GetCourierRouteQuery request, CancellationToken cancellationToken) => CourierRouteViews.RouteAsync(db, caller, request.Code, cancellationToken);
}
