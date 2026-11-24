using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Warehouse.Stock;

/// <summary>`GET /stock` : les lignes de stock, par quantité.</summary>
public sealed record GetStockQuery(StockListFilter Filter) : IRequest<Paginated<StockListItemDto>>;

internal sealed class GetStockHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetStockQuery, Paginated<StockListItemDto>>
{
    public async Task<Paginated<StockListItemDto>> Handle(GetStockQuery request, CancellationToken cancellationToken)
    {
        var (where, thresholdOf) = await StockRows.FilterAsync(db, caller, request.Filter, cancellationToken);
        var paging = request.Filter.Paging;
        var total = await where.CountAsync(cancellationToken);
        var ordered = request.Filter.Order == "desc" ? where.OrderByDescending(s => s.Quantity) : where.OrderBy(s => s.Quantity);
        var rows = await StockRows.Project(ordered.Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize)).ToListAsync(cancellationToken);
        return new Paginated<StockListItemDto>(rows.Select(r => StockRows.Fill(new StockListItemDto(), r, thresholdOf(r.AgencyId))).ToList(), total, paging.Page, paging.PageSize);
    }
}
