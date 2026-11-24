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

/// <summary>`PATCH /delivery-types/:id` : on ne retire pas le défaut sans en désigner un autre.</summary>
public sealed record UpdateDeliveryTypeCommand : DeliveryTypeFields, IRequest<DeliveryTypeListItemDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Id { get; init; } = string.Empty;
}

public sealed class UpdateDeliveryTypeValidator : DeliveryTypeFieldsValidator<UpdateDeliveryTypeCommand>;

internal sealed class UpdateDeliveryTypeHandler(IAppDbContext db, IMapper mapper) : IRequestHandler<UpdateDeliveryTypeCommand, DeliveryTypeListItemDto>
{
    public async Task<DeliveryTypeListItemDto> Handle(UpdateDeliveryTypeCommand input, CancellationToken cancellationToken)
    {
        var type = await db.DeliveryTypes.FirstOrDefaultAsync(t => t.Id == input.Id && t.DeletedAt == null, cancellationToken)
            ?? throw BusinessException.NotFound("Type de livraison introuvable.");
        await DeliveryTypeRows.AssertNameFreeAsync(db, input.Name!, input.Id, cancellationToken);
        var makeDefault = input.IsDefault ?? false;
        if (type.IsDefault && !makeDefault)
        {
            throw BusinessException.Validation("Désignez d’abord un autre type par défaut : la création de colis en exige un.", new { field = "isDefault" });
        }
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (makeDefault && !type.IsDefault) await DeliveryTypeRows.ClearDefaultAsync(db, input.Id, cancellationToken);
        type.Name = input.Name!;
        type.Multiplier = input.Multiplier;
        type.IsActive = input.IsActive ?? true;
        type.IsDefault = makeDefault;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await DeliveryTypeRows.LoadAsync(db, mapper, type.Id, cancellationToken);
    }
}
