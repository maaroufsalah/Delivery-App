using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.Parcels;
using Delivery.Application.Warehouse.Stock;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Warehouse.Orders;

/// <summary>`GET /orders` : les commandes, de la plus récente (ordre « asc » par défaut, comme Speedy).</summary>
public sealed record GetOrdersQuery(OrderListFilter Filter) : IRequest<Paginated<OrderListItemDto>>;

internal sealed class GetOrdersHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetOrdersQuery, Paginated<OrderListItemDto>>
{
    public async Task<Paginated<OrderListItemDto>> Handle(GetOrdersQuery request, CancellationToken cancellationToken)
    {
        var where = OrderRows.Filter(db.Orders.AsNoTracking(), caller, request.Filter);
        var paging = request.Filter.Paging;
        var total = await where.CountAsync(cancellationToken);
        var rows = await OrderRows.Project(where.Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize)).ToListAsync(cancellationToken);
        return new Paginated<OrderListItemDto>(rows.Select(r => OrderRows.Fill(new OrderListItemDto(), r)).ToList(), total, paging.Page, paging.PageSize);
    }
}
