using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.Users.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.DeliveryTypes.Commands;

/// <summary>`DELETE /delivery-types/:id`.</summary>
public sealed record RemoveDeliveryTypeCommand(string Id) : IRequest<IdResult>;

internal sealed class RemoveDeliveryTypeHandler(IAppDbContext db, IClock clock) : IRequestHandler<RemoveDeliveryTypeCommand, IdResult>
{
    public async Task<IdResult> Handle(RemoveDeliveryTypeCommand request, CancellationToken cancellationToken)
    {
        var type = await db.DeliveryTypes.FirstOrDefaultAsync(t => t.Id == request.Id && t.DeletedAt == null, cancellationToken)
            ?? throw BusinessException.NotFound("Type de livraison introuvable.");
        if (type.IsDefault) throw BusinessException.Conflict("Le type par défaut ne se supprime pas.", new { field = "isDefault" });
        References.AssertDeletable($"le type « {type.Name} »",
            References.Of("parcels", await db.Parcels.CountAsync(p => p.DeliveryTypeId == request.Id, cancellationToken)));
        type.DeletedAt = clock.UtcNow;
        type.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return new IdResult(request.Id);
    }
}
