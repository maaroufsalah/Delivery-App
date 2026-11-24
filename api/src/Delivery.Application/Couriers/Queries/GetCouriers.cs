using AutoMapper;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Couriers.Queries;

/// <summary>`GET /couriers` : vide pour un vendeur, réduit à soi pour un livreur.</summary>
public sealed record GetCouriersQuery(ListQuery Query, IReadOnlyList<string>? AgencyIds, IReadOnlyList<string>? CityIds, string? Validated) : IRequest<Paginated<CourierListItemDto>>;

internal sealed class GetCouriersHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<GetCouriersQuery, Paginated<CourierListItemDto>>
{
    public async Task<Paginated<CourierListItemDto>> Handle(GetCouriersQuery request, CancellationToken cancellationToken)
    {
        if (caller.ClientId is not null && !caller.IsSuperAdmin()) return Paginated<CourierListItemDto>.Of([], 0, request.Query);
        var where = Filter(db.Couriers.AsNoTracking(), request, caller);
        var total = await where.CountAsync(cancellationToken);
        var page = Sorting.OrderByKey(where, request.Query, CourierRows.Sorts, "lastName").Skip(request.Query.Skip).Take(request.Query.PageSize);
        return Paginated<CourierListItemDto>.Of(await CourierRows.ProjectAsync<CourierListItemDto>(page, mapper, cancellationToken), total, request.Query);
    }

    internal static IQueryable<Courier> Filter(IQueryable<Courier> couriers, GetCouriersQuery request, ICurrentUser caller)
    {
        var where = couriers.Where(c => c.DeletedAt == null);
        if (request.Query.ActiveFilter is { } active) where = where.Where(c => c.IsActive == active);
        if (caller.CourierId is { } self && !caller.IsSuperAdmin()) where = where.Where(c => c.Id == self);
        if (request.AgencyIds is { Count: > 0 } agencyIds) where = where.Where(c => c.AgencyId != null && agencyIds.Contains(c.AgencyId));
        if (request.CityIds is { Count: > 0 } cityIds) where = where.Where(c => cityIds.Contains(c.CityId));
        where = request.Validated switch
        {
            "true" => where.Where(c => c.ValidatedAt != null),
            "false" => where.Where(c => c.ValidatedAt == null && c.RejectedAt == null),
            "rejected" => where.Where(c => c.RejectedAt != null),
            _ => where,
        };
        if (!string.IsNullOrEmpty(request.Query.Search))
        {
            var pattern = Like.Contains(request.Query.Search);
            var raw = request.Query.Search;
            where = where.Where(c => EF.Functions.ILike(c.FirstName, pattern) || EF.Functions.ILike(c.LastName, pattern) || c.Phone.Contains(raw));
        }
        return where;
    }
}
