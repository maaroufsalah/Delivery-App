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

/// <summary>`DELETE /orders/:code` : une commande non transformée.</summary>
public sealed record RemoveOrderCommand(string Code) : IRequest<OrderCodeDto>;

public sealed record OrderCodeDto(string Code);

internal sealed class RemoveOrderHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RemoveOrderCommand, OrderCodeDto>
{
    public async Task<OrderCodeDto> Handle(RemoveOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await WarehouseScope.Orders(db.Orders, caller).Include(o => o.Lines).FirstOrDefaultAsync(o => o.Code == request.Code, cancellationToken)
                    ?? throw BusinessException.NotFound($"Commande introuvable : {request.Code}");
        if (order.Status == OrderStatus.PARCEL_CREATED) throw BusinessException.Conflict("Une commande transformée en colis ne se supprime pas.");
        db.OrderLines.RemoveRange(order.Lines);
        db.Orders.Remove(order);
        await db.SaveChangesAsync(cancellationToken);
        return new OrderCodeDto(request.Code);
    }
}
