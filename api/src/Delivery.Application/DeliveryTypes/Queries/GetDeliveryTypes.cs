using AutoMapper;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.DeliveryTypes.Queries;

/// <summary>`GET /delivery-types`.</summary>
public sealed record GetDeliveryTypesQuery(ListQuery Query) : IRequest<Paginated<DeliveryTypeListItemDto>>;

internal sealed class GetDeliveryTypesHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<GetDeliveryTypesQuery, Paginated<DeliveryTypeListItemDto>>
{
    public async Task<Paginated<DeliveryTypeListItemDto>> Handle(GetDeliveryTypesQuery request, CancellationToken cancellationToken)
    {
        caller.AssertCanSeeInternalPricing("Types de livraison");
        var where = Filter(db.DeliveryTypes.AsNoTracking(), request.Query);
        var total = await where.CountAsync(cancellationToken);
        var rows = await mapper.ProjectTo<DeliveryTypeRow>(Sorting.OrderByKey(where, request.Query, DeliveryTypeRows.Sorts, "name").Skip(request.Query.Skip).Take(request.Query.PageSize))
            .ToListAsync(cancellationToken);
        return Paginated<DeliveryTypeListItemDto>.Of(rows.Select(r => r.ToDto()).ToList(), total, request.Query);
    }

    internal static IQueryable<DeliveryType> Filter(IQueryable<DeliveryType> types, ListQuery query)
    {
        var where = types.Where(t => t.DeletedAt == null);
        if (query.ActiveFilter is { } active) where = where.Where(t => t.IsActive == active);
        if (!string.IsNullOrEmpty(query.Search))
        {
            var pattern = Like.Contains(query.Search);
            where = where.Where(t => EF.Functions.ILike(t.Name, pattern));
        }
        return where;
    }
}
