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

/// <summary>`POST /orders/:code/confirm` : le brouillon devient confirmé.</summary>
public sealed record ConfirmOrderCommand(string Code) : IRequest<OrderDetailDto>;

internal sealed class ConfirmOrderHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<ConfirmOrderCommand, OrderDetailDto>
{
    public async Task<OrderDetailDto> Handle(ConfirmOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await WarehouseScope.Orders(db.Orders, caller).Include(o => o.Lines).FirstOrDefaultAsync(o => o.Code == request.Code, cancellationToken)
                    ?? throw BusinessException.NotFound($"Commande introuvable : {request.Code}");
        if (order.Status != OrderStatus.DRAFT) throw BusinessException.Conflict("Seule une commande en brouillon se confirme.", new { reason = "order-not-draft" });
        if (order.Lines.Count == 0) throw BusinessException.Validation("Une commande sans ligne ne se confirme pas.");
        order.Status = OrderStatus.CONFIRMED;
        await db.SaveChangesAsync(cancellationToken);
        return await OrderRows.DetailAsync(db, caller, request.Code, cancellationToken);
    }
}
