using AutoMapper;
using AutoMapper.QueryableExtensions;
using Delivery.Application.Cities.Dtos;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Cities.Queries;

/// <summary>`GET /cities` : écran d'administration des villes, paginé et trié côté serveur.</summary>
public sealed record GetCitiesQuery(ListQuery Query, IReadOnlyList<string>? ZoneIds) : IRequest<Paginated<CityListItemDto>>;

internal sealed class GetCitiesHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<GetCitiesQuery, Paginated<CityListItemDto>>
{
    public async Task<Paginated<CityListItemDto>> Handle(GetCitiesQuery request, CancellationToken cancellationToken)
    {
        caller.AssertCanSeeInternalPricing("Villes");
        var where = Filter(db.Cities.AsNoTracking(), request);
        var ordered = Sorting.OrderByKey(where, request.Query, CityRows.Sorts, "name");
        var total = await where.CountAsync(cancellationToken);
        var rows = await ordered.Skip(request.Query.Skip).Take(request.Query.PageSize)
            .ProjectTo<CityListItemDto>(mapper.ConfigurationProvider).ToListAsync(cancellationToken);
        return Paginated<CityListItemDto>.Of(await CityRows.WithCountsAsync(db, rows, cancellationToken), total, request.Query);
    }

    internal static IQueryable<City> Filter(IQueryable<City> cities, GetCitiesQuery request)
    {
        var query = cities.Where(c => c.DeletedAt == null);
        if (request.Query.ActiveFilter is { } active) query = query.Where(c => c.IsActive == active);
        if (request.ZoneIds is { Count: > 0 } zoneIds) query = query.Where(c => zoneIds.Contains(c.ZoneId));
        if (!string.IsNullOrEmpty(request.Query.Search))
        {
            var pattern = Like.Contains(request.Query.Search);
            query = query.Where(c => EF.Functions.ILike(c.Name, pattern) || EF.Functions.ILike(c.Code, pattern));
        }
        return query;
    }
}
