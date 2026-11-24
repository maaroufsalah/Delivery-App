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

/// <summary>`GET /courier/me/routes` (`courierRoutesQuerySchema`) : brouillon ou tournées parties ; l'historique (`closed`) rend une liste vide.</summary>
public sealed record GetCourierRoutesQuery(string? Status, string? From, string? Kind) : IRequest<IReadOnlyList<CourierRouteDto>>;

public sealed class GetCourierRoutesValidator : ZodValidator<GetCourierRoutesQuery>
{
    public GetCourierRoutesValidator()
    {
        RuleFor(x => x.Status).OneOf(["draft", "active", "closed"]);
        RuleFor(x => x.Kind).OneOf(["return"]);
    }
}

internal sealed class GetCourierRoutesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetCourierRoutesQuery, IReadOnlyList<CourierRouteDto>>
{
    public async Task<IReadOnlyList<CourierRouteDto>> Handle(GetCourierRoutesQuery request, CancellationToken cancellationToken)
    {
        var courierId = CourierRouteViews.CourierIdOf(caller);
        var status = request.Status ?? "active";
        if (status == "closed") return [];
        var routes = db.Routes.AsNoTracking().Where(r => r.CourierId == courierId && r.Status == (status == "draft" ? RouteStatus.DRAFT : RouteStatus.DISPATCHED));
        routes = request.Kind == "return" ? routes.Where(r => r.Kind == RouteKind.RETURN) : routes.Where(r => r.Kind == RouteKind.DELIVERY || r.Kind == RouteKind.PICKUP);
        var from = request.From?.Trim();
        if (!string.IsNullOrEmpty(from))
        {
            // `dayRange` : à partir de minuit à Casablanca, pas de minuit UTC.
            var start = Periods.ZonedDayStart(from, ClockDays.Zone);
            routes = routes.Where(r => r.CreatedAt >= start);
        }
        var rows = await CourierRouteViews.LoadAsync(db, routes.OrderByDescending(r => r.CreatedAt).Take(20), cancellationToken);
        return rows.Select(CourierRouteViews.ToRoute).ToList();
    }
}
