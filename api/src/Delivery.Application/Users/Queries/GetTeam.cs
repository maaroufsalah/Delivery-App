using AutoMapper;
using AutoMapper.QueryableExtensions;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Users.Dtos;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Users.Queries;

/// <summary>
/// `GET /users/team` : le personnel de MON agence. Sans agence, le
/// super-administrateur voit tout le personnel, les autres se voient seuls.
/// </summary>
public sealed record GetTeamQuery(ListQuery Query) : IRequest<Paginated<UserListItemDto>>;

internal sealed class GetTeamHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<GetTeamQuery, Paginated<UserListItemDto>>
{
    public async Task<Paginated<UserListItemDto>> Handle(GetTeamQuery request, CancellationToken cancellationToken)
    {
        var staffRoles = new[] { RoleName.STAFF, RoleName.SUPER_ADMIN };
        var query = db.Users.AsNoTracking().Where(u => u.DeletedAt == null && u.Roles.Any(r => staffRoles.Contains(r.Role)));
        if (request.Query.ActiveFilter is { } active) query = query.Where(u => u.IsActive == active);
        var agencyId = caller.AgencyId;
        if (agencyId is not null) query = query.Where(u => u.AgencyId == agencyId || u.Agencies.Any(a => a.AgencyId == agencyId));
        else if (!caller.IsSuperAdmin()) query = query.Where(u => u.Id == caller.UserId);
        query = GetUsersHandler.Search(query, request.Query.Search);

        var ordered = Sorting.OrderByKey(query, request.Query, GetUsersHandler.Sorts, "lastName");
        var total = await query.CountAsync(cancellationToken);
        var items = await ordered.Skip(request.Query.Skip).Take(request.Query.PageSize)
            .ProjectTo<UserListItemDto>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
        return Paginated<UserListItemDto>.Of(items, total, request.Query);
    }
}
