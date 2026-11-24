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

/// <summary>`GET /routes` : par changement de statut, ou les plus récents (`recent=true`).</summary>
public sealed record GetRoutesQuery(RouteListFilter Filter) : IRequest<Paginated<RouteListItemDto>>;

internal sealed class GetRoutesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetRoutesQuery, Paginated<RouteListItemDto>>
{
    public async Task<Paginated<RouteListItemDto>> Handle(GetRoutesQuery request, CancellationToken cancellationToken)
    {
        var paging = request.Filter.Paging;
        var where = RouteRows.Filter(db.Routes.AsNoTracking(), caller, request.Filter);
        var total = await where.CountAsync(cancellationToken);
        var ordered = request.Filter.Recent == "true" ? where.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Code) : where.OrderByDescending(r => r.StatusAt);
        var rows = await RouteRows.Project(ordered.Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize)).ToListAsync(cancellationToken);
        var removed = await RouteRows.RemovedCountsAsync(db, rows.Select(r => r.Id).ToList(), cancellationToken);
        return new Paginated<RouteListItemDto>(rows.Select(r => RouteRows.Fill(new RouteListItemDto(), r, removed.GetValueOrDefault(r.Id))).ToList(), total, paging.Page, paging.PageSize);
    }
}
