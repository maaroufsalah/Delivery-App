using AutoMapper;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Zones.Dtos;
using Delivery.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Zones.Queries;

/// <summary>`GET /zones` : zones et leurs tarifs, paginées.</summary>
public sealed record GetZonesQuery(ListQuery Query) : IRequest<Paginated<ZoneListItemDto>>;

internal sealed class GetZonesHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<GetZonesQuery, Paginated<ZoneListItemDto>>
{
    public async Task<Paginated<ZoneListItemDto>> Handle(GetZonesQuery request, CancellationToken cancellationToken)
    {
        caller.AssertCanSeeInternalPricing("Zones");
        var where = Filter(db.Zones.AsNoTracking(), request.Query);
        var total = await where.CountAsync(cancellationToken);
        var rows = await mapper.ProjectTo<ZoneRow>(Sorting.OrderByKey(where, request.Query, ZoneRows.Sorts, "name").Skip(request.Query.Skip).Take(request.Query.PageSize))
            .ToListAsync(cancellationToken);
        return Paginated<ZoneListItemDto>.Of(rows.Select(r => r.ToDto()).ToList(), total, request.Query);
    }

    internal static IQueryable<Zone> Filter(IQueryable<Zone> zones, ListQuery query)
    {
        var where = zones.Where(z => z.DeletedAt == null);
        if (query.ActiveFilter is { } active) where = where.Where(z => z.IsActive == active);
        if (!string.IsNullOrEmpty(query.Search))
        {
            var pattern = Like.Contains(query.Search);
            where = where.Where(z => EF.Functions.ILike(z.Name, pattern));
        }
        return where;
    }
}
