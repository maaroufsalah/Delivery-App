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

/// <summary>`POST /orders/:code/cancel` : tant qu'aucun colis n'est créé.</summary>
public sealed record CancelOrderCommand(string Code) : IRequest<OrderDetailDto>;

internal sealed class CancelOrderHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CancelOrderCommand, OrderDetailDto>
{
    public async Task<OrderDetailDto> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await WarehouseScope.Orders(db.Orders, caller).FirstOrDefaultAsync(o => o.Code == request.Code, cancellationToken)
                    ?? throw BusinessException.NotFound($"Commande introuvable : {request.Code}");
        if (order.Status == OrderStatus.PARCEL_CREATED) throw BusinessException.Conflict("Le colis est déjà créé : annulez le colis.", new { reason = "order-has-parcel" });
        order.Status = OrderStatus.CANCELLED;
        await db.SaveChangesAsync(cancellationToken);
        return await OrderRows.DetailAsync(db, caller, request.Code, cancellationToken);
    }
}
