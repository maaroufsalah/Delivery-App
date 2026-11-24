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

/// <summary>`GET /parcels/stats` : compteurs de la sélection courante.</summary>
public sealed record GetParcelStatsQuery(ParcelListFilter Filter) : IRequest<ParcelStatsDto>;

internal sealed class GetParcelStatsHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetParcelStatsQuery, ParcelStatsDto>
{
    public async Task<ParcelStatsDto> Handle(GetParcelStatsQuery request, CancellationToken cancellationToken)
    {
        var (where, window) = await ParcelFilters.InPeriodAsync(db, caller, request.Filter, clock, cancellationToken);
        var total = await where.CountAsync(cancellationToken);
        var byStatus = await where.GroupBy(p => p.StatusId).Select(g => new { StatusId = g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        var collected = await where.Where(p => p.StatusId == StatusCatalog.Delivered).SumAsync(p => (decimal?)p.CodAmount, cancellationToken);
        int delivered = 0, failed = 0, inTransit = 0, returned = 0;
        foreach (var row in byStatus)
        {
            var group = StatusCatalog.GroupOf(row.StatusId);
            if (row.StatusId == StatusCatalog.Delivered) delivered += row.Count;
            if (row.StatusId == StatusCatalog.Returned) returned += row.Count;
            if (group is "failure" or "retry") failed += row.Count;
            if (group is "transit" or "pivot") inTransit += row.Count;
        }
        var closed = delivered + returned;
        return new ParcelStatsDto(total, delivered, inTransit, failed, Money.ToCents(collected), closed == 0 ? 0 : (double)delivered / closed)
        {
            Window = window is null ? null : new ParcelStatsWindow(window.Period, window.From, window.To, window.Granularity),
        };
    }
}
