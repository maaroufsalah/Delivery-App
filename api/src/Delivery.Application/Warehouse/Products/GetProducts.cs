using System.Text.Json.Serialization;
using AutoMapper;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Persistence;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Warehouse.Products;

/// <summary>`GET /products` : les produits de l'entrepôt, par nom.</summary>
public sealed record GetProductsQuery(ProductListFilter Filter) : IRequest<Paginated<ProductListItemDto>>;

internal sealed class GetProductsHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<GetProductsQuery, Paginated<ProductListItemDto>>
{
    public async Task<Paginated<ProductListItemDto>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var where = Filter(db.Products.AsNoTracking(), caller, request.Filter);
        var paging = request.Filter.Paging;
        var total = await where.CountAsync(cancellationToken);
        var ordered = request.Filter.Order == "desc" ? where.OrderByDescending(p => p.Name) : where.OrderBy(p => p.Name);
        var items = await ProductRows.ListAsync(mapper, ordered.Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize), cancellationToken);
        return new Paginated<ProductListItemDto>(items, total, paging.Page, paging.PageSize);
    }

    internal static IQueryable<Product> Filter(IQueryable<Product> products, ICurrentUser caller, ProductListFilter filter)
    {
        var where = WarehouseScope.Products(products, caller).Where(p => p.DeletedAt == null);
        if (filter.ClientId is { Count: > 0 } clients && !WarehouseScope.IsMerchant(caller)) where = where.Where(p => clients.Contains(p.ClientId));
        if (filter.IsActive is "true" or "false") { var active = filter.IsActive == "true"; where = where.Where(p => p.IsActive == active); }
        if (filter.AgencyId is { Count: > 0 } agencies) where = where.Where(p => p.StockItems.Any(s => agencies.Contains(s.AgencyId)));
        if (!string.IsNullOrEmpty(filter.Paging.Search))
        {
            var pattern = Like.Contains(filter.Paging.Search);
            where = where.Where(p => EF.Functions.ILike(p.Name, pattern) || EF.Functions.ILike(p.Reference!, pattern));
        }
        return where;
    }
}
