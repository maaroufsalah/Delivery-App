using AutoMapper;
using Delivery.Application.Clients.Dtos;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Clients.Commands;

/// <summary>`POST /clients/:id/discounts` : une remise par ville, par zone ou sur tout.</summary>
public sealed record AddClientDiscountCommand : ClientDiscountFields, IRequest<ClientDetailDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string ClientId { get; init; } = string.Empty;
}

public sealed class AddClientDiscountValidator : ClientDiscountFieldsValidator<AddClientDiscountCommand>;

internal sealed class AddClientDiscountHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<AddClientDiscountCommand, ClientDetailDto>
{
    public async Task<ClientDetailDto> Handle(AddClientDiscountCommand input, CancellationToken cancellationToken)
    {
        await ClientRows.FindAsync(db, input.ClientId, cancellationToken);
        var (zoneIds, cityIds) = await ClientDiscounts.TargetsAsync(db, input, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await ClientDiscounts.AssertNoDuplicateAsync(db, input.ClientId, zoneIds, cityIds, input.IsActive ?? true, null, cancellationToken);
        var discount = new ClientDiscount
        {
            ClientId = input.ClientId,
            ZoneIds = zoneIds,
            FlatRate = ClientDiscounts.Decimal(input.FlatRate),
            SameCityRate = ClientDiscounts.Decimal(input.SameCityRate),
            PickupFee = ClientDiscounts.Decimal(input.PickupFee),
            IsActive = input.IsActive ?? true,
        };
        foreach (var cityId in cityIds) discount.Cities.Add(new ClientDiscountCity { DiscountId = discount.Id, CityId = cityId });
        db.ClientDiscounts.Add(discount);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ClientRows.DetailAsync(db, caller, mapper, input.ClientId, cancellationToken);
    }
}
