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

/// <summary>`DELETE /routes/:code` : un brouillon vide.</summary>
public sealed record DeleteRouteCommand(string Code) : IRequest<RouteCodeDto>;

internal sealed class DeleteRouteHandler(IAppDbContext db, ICurrentUser caller, ILogger<DeleteRouteHandler> logger) : IRequestHandler<DeleteRouteCommand, RouteCodeDto>
{
    public async Task<RouteCodeDto> Handle(DeleteRouteCommand request, CancellationToken cancellationToken)
    {
        var route = await RouteRows.FindAsync(db, caller, request.Code, cancellationToken, tracking: true);
        RouteWrites.AssertDraft(route.Status);
        var parcels = await db.RouteParcels.CountAsync(l => l.RouteId == route.Id, cancellationToken);
        var notes = await db.RouteNotes.CountAsync(l => l.RouteId == route.Id, cancellationToken);
        if (parcels + notes > 0)
        {
            throw BusinessException.Conflict($"{route.Code} porte encore {parcels} colis et {notes} bon(s) : retirez-les avant de le supprimer.", new { reason = "route-not-empty", parcels, notes });
        }
        db.Routes.Remove(route);
        await db.SaveChangesAsync(cancellationToken);
        await RouteWrites.NotifyWithdrawnAsync(db, logger, route.CourierId, route.Code, cancellationToken);
        return new RouteCodeDto(route.Code);
    }
}
