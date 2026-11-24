using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.BulkJobs;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Routes;

/// <summary>`POST /routes/auto-close-sweep` : clôt les circuits de livraison ouverts déjà complets.</summary>
public sealed record SweepAutoCloseCommand : IRequest<IReadOnlyList<AutoClosedRouteDto>>;

internal sealed class SweepAutoCloseHandler(IAppDbContext db, ICurrentUser caller, ILogger<SweepAutoCloseHandler> logger) : IRequestHandler<SweepAutoCloseCommand, IReadOnlyList<AutoClosedRouteDto>>
{
    public async Task<IReadOnlyList<AutoClosedRouteDto>> Handle(SweepAutoCloseCommand request, CancellationToken cancellationToken)
    {
        var open = await db.Routes.AsNoTracking().Where(r => r.Kind == RouteKind.DELIVERY && (r.Status == RouteStatus.DISPATCHED || r.Status == RouteStatus.DELIVERING))
            .OrderBy(r => r.CreatedAt).Select(r => new { r.Id, r.Code }).ToListAsync(cancellationToken);
        var closed = new List<AutoClosedRouteDto>();
        foreach (var route in open)
        {
            if (await RouteClosing.AutoCloseDeliveryAsync(db, caller, logger, route.Id, cancellationToken) is { } done) closed.Add(new AutoClosedRouteDto(route.Code, done.Delivered, done.Returned, done.Total));
        }
        return closed;
    }
}
