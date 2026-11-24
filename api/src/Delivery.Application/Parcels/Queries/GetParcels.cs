using AutoMapper;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Parcels.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Parcels.Queries;

/// <summary>`GET /parcels` : la liste de MA portée, du plus récent changement de statut au plus ancien.</summary>
public sealed record GetParcelsQuery(ParcelListFilter Filter) : IRequest<Paginated<ParcelListItemDto>>;

internal sealed class GetParcelsHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper, IClock clock) : IRequestHandler<GetParcelsQuery, Paginated<ParcelListItemDto>>
{
    public async Task<Paginated<ParcelListItemDto>> Handle(GetParcelsQuery request, CancellationToken cancellationToken)
    {
        var filter = request.Filter;
        var (where, _) = await ParcelFilters.InPeriodAsync(db, caller, filter, clock, cancellationToken);
        var total = await where.CountAsync(cancellationToken);
        var ordered = filter.Recent == "true"
            ? where.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Code)
            : where.OrderByDescending(p => p.StatusAt).ThenBy(p => p.Code);
        var rows = await mapper.ProjectTo<ParcelRow>(ordered.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)).ToListAsync(cancellationToken);
        var outcomes = await CourierOutcomesAsync(db, caller, rows, cancellationToken);
        var items = rows.Select(row =>
        {
            var item = ParcelViews.ToListItem(row, caller);
            if (outcomes.TryGetValue(row.Id, out var courierOutcomes)) item.Extra["courierOutcomes"] = courierOutcomes;
            return item;
        }).ToList();
        return new Paginated<ParcelListItemDto>(items, total, filter.Page, filter.PageSize);
    }

    /// <summary>`courierOutcomesForPage` : pour un livreur, les issues déclarables selon le circuit ouvert.</summary>
    internal static async Task<Dictionary<string, int[]>> CourierOutcomesAsync(IAppDbContext db, ICurrentUser caller, IReadOnlyList<ParcelRow> parcels, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, int[]>();
        if (caller.CourierId is not { } courierId || parcels.Count == 0) return result;
        var ids = parcels.Select(p => p.Id).ToList();
        var links = await db.RouteParcels.AsNoTracking()
            .Where(l => ids.Contains(l.ParcelId) && l.Route.CourierId == courierId && l.Route.Status != RouteStatus.CLOSED)
            .OrderByDescending(l => l.RouteId)
            .Select(l => new { l.ParcelId, l.Route.Kind })
            .ToListAsync(cancellationToken);
        var reserved = (await db.ReturnHandoverItems.AsNoTracking()
            .Where(i => i.ReservedParcelId != null && ids.Contains(i.ReservedParcelId) && i.Handover.Status == ReturnHandoverStatus.AWAITING_RECEIPT)
            .Select(i => i.ReservedParcelId!).ToListAsync(cancellationToken)).ToHashSet();
        var kindOf = new Dictionary<string, RouteKind>();
        foreach (var link in links) kindOf.TryAdd(link.ParcelId, link.Kind);
        foreach (var parcel in parcels)
        {
            if (!kindOf.TryGetValue(parcel.Id, out var kind) || (kind == RouteKind.DELIVERY && reserved.Contains(parcel.Id))) result[parcel.Id] = [];
            else result[parcel.Id] = StatusCatalog.CourierOutcomesFor(parcel.StatusId, kind.ToString());
        }
        return result;
    }
}
