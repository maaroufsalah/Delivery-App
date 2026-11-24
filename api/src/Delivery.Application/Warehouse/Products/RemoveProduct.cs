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

/// <summary>`DELETE /products/:id` : refusé tant qu'il reste du stock.</summary>
public sealed record RemoveProductCommand(string Id) : IRequest<IdDto>;

internal sealed class RemoveProductHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<RemoveProductCommand, IdDto>
{
    public async Task<IdDto> Handle(RemoveProductCommand request, CancellationToken cancellationToken)
    {
        var product = await WarehouseScope.Products(db.Products, caller).FirstOrDefaultAsync(p => p.Id == request.Id && p.DeletedAt == null, cancellationToken)
                      ?? throw BusinessException.NotFound("Produit introuvable.");
        var remaining = await db.StockItems.Where(s => s.ProductId == product.Id).SumAsync(s => s.Quantity, cancellationToken);
        if (remaining > 0) throw BusinessException.Conflict($"Ce produit a encore {remaining} unité(s) en stock.", new { reason = "product-has-stock", quantity = remaining });
        product.DeletedAt = clock.UtcNow;
        product.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return new IdDto(product.Id);
    }
}
