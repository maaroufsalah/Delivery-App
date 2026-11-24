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

/// <summary>`GET /routes/stats`.</summary>
public sealed record GetRouteStatsQuery(RouteListFilter Filter) : IRequest<RouteStatsDto>;

internal sealed class GetRouteStatsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetRouteStatsQuery, RouteStatsDto>
{
    public async Task<RouteStatsDto> Handle(GetRouteStatsQuery request, CancellationToken cancellationToken)
    {
        var where = RouteRows.Filter(db.Routes.AsNoTracking(), caller, request.Filter);
        var total = await where.CountAsync(cancellationToken);
        var open = await where.CountAsync(r => r.Status != RouteStatus.CLOSED, cancellationToken);
        var links = where.SelectMany(r => r.Parcels);
        return new RouteStatsDto(total, open, await links.CountAsync(cancellationToken), await links.CountAsync(l => l.Confirmed, cancellationToken));
    }
}
