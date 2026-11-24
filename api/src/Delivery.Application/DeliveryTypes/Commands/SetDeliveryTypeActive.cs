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

/// <summary>`PATCH /delivery-types/:id/active` : le type par défaut reste actif.</summary>
public sealed record SetDeliveryTypeActiveCommand : IRequest<DeliveryTypeListItemDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Id { get; init; } = string.Empty;

    public bool? IsActive { get; init; }
}

public sealed class SetDeliveryTypeActiveValidator : ZodValidator<SetDeliveryTypeActiveCommand>
{
    public SetDeliveryTypeActiveValidator() => RuleFor(x => x.IsActive).Required("boolean");
}

internal sealed class SetDeliveryTypeActiveHandler(IAppDbContext db, IMapper mapper) : IRequestHandler<SetDeliveryTypeActiveCommand, DeliveryTypeListItemDto>
{
    public async Task<DeliveryTypeListItemDto> Handle(SetDeliveryTypeActiveCommand request, CancellationToken cancellationToken)
    {
        var type = await db.DeliveryTypes.FirstOrDefaultAsync(t => t.Id == request.Id && t.DeletedAt == null, cancellationToken)
            ?? throw BusinessException.NotFound("Type de livraison introuvable.");
        if (type.IsDefault && request.IsActive == false)
        {
            throw BusinessException.Validation("Le type par défaut ne peut pas être désactivé.", new { field = "isActive" });
        }
        type.IsActive = request.IsActive!.Value;
        await db.SaveChangesAsync(cancellationToken);
        return await DeliveryTypeRows.LoadAsync(db, mapper, type.Id, cancellationToken);
    }
}
