using AutoMapper;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Agencies.Queries;

/// <summary>`GET /agencies` : un agent ne voit que son agence.</summary>
public sealed record GetAgenciesQuery(ListQuery Query) : IRequest<Paginated<AgencyListItemDto>>;

internal sealed class GetAgenciesHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<GetAgenciesQuery, Paginated<AgencyListItemDto>>
{
    public async Task<Paginated<AgencyListItemDto>> Handle(GetAgenciesQuery request, CancellationToken cancellationToken)
    {
        var where = Filter(db.Agencies.AsNoTracking(), request.Query, caller);
        var total = await where.CountAsync(cancellationToken);
        var rows = await mapper.ProjectTo<AgencyRow>(Sorting.OrderByKey(where, request.Query, AgencyRows.Sorts, "name").Skip(request.Query.Skip).Take(request.Query.PageSize))
            .ToListAsync(cancellationToken);
        var headquarters = await AgencyRows.HeadquartersIdAsync(db, cancellationToken);
        return Paginated<AgencyListItemDto>.Of(rows.Select(r => r.ToDto(headquarters)).ToList(), total, request.Query);
    }

    internal static IQueryable<Agency> Filter(IQueryable<Agency> agencies, ListQuery query, ICurrentUser caller)
    {
        var where = agencies.Where(a => a.DeletedAt == null);
        if (query.ActiveFilter is { } active) where = where.Where(a => a.IsActive == active);
        if (!caller.IsSuperAdmin() && caller.AgencyId is { } agencyId) where = where.Where(a => a.Id == agencyId);
        if (!string.IsNullOrEmpty(query.Search))
        {
            var pattern = Like.Contains(query.Search);
            where = where.Where(a => EF.Functions.ILike(a.Name, pattern) || EF.Functions.ILike(a.City.Name, pattern));
        }
        return where;
    }
}
