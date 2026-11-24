using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Routes;

/// <summary>`DELETE /routes/:code/parcels/:parcelId` (bureau).</summary>
public sealed record RemoveRouteParcelCommand(string Code, string ParcelId) : IRequest<RouteDetailDto>;

internal sealed class RemoveRouteParcelHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RemoveRouteParcelCommand, RouteDetailDto>
{
    public async Task<RouteDetailDto> Handle(RemoveRouteParcelCommand request, CancellationToken cancellationToken)
    {
        var route = await RouteRows.FindAsync(db, caller, request.Code, cancellationToken);
        RouteWrites.AssertDraft(route.Status);
        await RouteWrites.RemoveParcelFromDraftAsync(db, caller, route, request.ParcelId, "office", cancellationToken);
        return await RouteRows.DetailAsync(db, caller, request.Code, cancellationToken);
    }
}
