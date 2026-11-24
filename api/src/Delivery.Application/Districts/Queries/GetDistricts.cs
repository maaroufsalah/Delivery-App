using AutoMapper;
using AutoMapper.QueryableExtensions;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Districts.Queries;

/// <summary>`GET /districts`.</summary>
public sealed record GetDistrictsQuery(ListQuery Query, IReadOnlyList<string>? CityIds) : IRequest<Paginated<DistrictListItemDto>>;

internal sealed class GetDistrictsHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<GetDistrictsQuery, Paginated<DistrictListItemDto>>
{
    public async Task<Paginated<DistrictListItemDto>> Handle(GetDistrictsQuery request, CancellationToken cancellationToken)
    {
        caller.AssertCanSeeInternalPricing("Quartiers");
        var where = Filter(db.Districts.AsNoTracking(), request);
        var total = await where.CountAsync(cancellationToken);
        var items = await Sorting.OrderByKey(where, request.Query, DistrictRows.Sorts, "name").Skip(request.Query.Skip).Take(request.Query.PageSize)
            .ProjectTo<DistrictListItemDto>(mapper.ConfigurationProvider).ToListAsync(cancellationToken);
        return Paginated<DistrictListItemDto>.Of(items, total, request.Query);
    }

    internal static IQueryable<District> Filter(IQueryable<District> districts, GetDistrictsQuery request)
    {
        var where = districts.Where(d => d.DeletedAt == null);
        if (request.Query.ActiveFilter is { } active) where = where.Where(d => d.IsActive == active);
        if (request.CityIds is { Count: > 0 } cityIds) where = where.Where(d => cityIds.Contains(d.CityId));
        if (!string.IsNullOrEmpty(request.Query.Search))
        {
            var pattern = Like.Contains(request.Query.Search);
            where = where.Where(d => EF.Functions.ILike(d.Name, pattern) || EF.Functions.ILike(d.City.Name, pattern));
        }
        return where;
    }
}
