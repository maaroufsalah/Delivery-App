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

/// <summary>`POST /delivery-types` : le nouveau type par défaut retire ce rôle aux autres.</summary>
public sealed record CreateDeliveryTypeCommand : DeliveryTypeFields, IRequest<DeliveryTypeListItemDto>;

public sealed class CreateDeliveryTypeValidator : DeliveryTypeFieldsValidator<CreateDeliveryTypeCommand>;

internal sealed class CreateDeliveryTypeHandler(IAppDbContext db, IMapper mapper) : IRequestHandler<CreateDeliveryTypeCommand, DeliveryTypeListItemDto>
{
    public async Task<DeliveryTypeListItemDto> Handle(CreateDeliveryTypeCommand input, CancellationToken cancellationToken)
    {
        await DeliveryTypeRows.AssertNameFreeAsync(db, input.Name!, null, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (input.IsDefault == true) await DeliveryTypeRows.ClearDefaultAsync(db, null, cancellationToken);
        var type = new DeliveryType { Name = input.Name!, Multiplier = input.Multiplier, IsActive = input.IsActive ?? true, IsDefault = input.IsDefault ?? false };
        db.DeliveryTypes.Add(type);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await DeliveryTypeRows.LoadAsync(db, mapper, type.Id, cancellationToken);
    }
}
