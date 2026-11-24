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

/// <summary>`PATCH /clients/:id/discounts/:discountId` : cibles remplacées.</summary>
public sealed record UpdateClientDiscountCommand : ClientDiscountFields, IRequest<ClientDetailDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string ClientId { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonIgnore]
    public string DiscountId { get; init; } = string.Empty;
}

public sealed class UpdateClientDiscountValidator : ClientDiscountFieldsValidator<UpdateClientDiscountCommand>;

internal sealed class UpdateClientDiscountHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<UpdateClientDiscountCommand, ClientDetailDto>
{
    public async Task<ClientDetailDto> Handle(UpdateClientDiscountCommand input, CancellationToken cancellationToken)
    {
        await ClientRows.FindAsync(db, input.ClientId, cancellationToken);
        var discount = await db.ClientDiscounts.Include(d => d.Cities).FirstOrDefaultAsync(d => d.Id == input.DiscountId && d.ClientId == input.ClientId, cancellationToken)
            ?? throw BusinessException.NotFound("Remise introuvable.");
        var (zoneIds, cityIds) = await ClientDiscounts.TargetsAsync(db, input, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await ClientDiscounts.AssertNoDuplicateAsync(db, input.ClientId, zoneIds, cityIds, input.IsActive ?? true, input.DiscountId, cancellationToken);
        discount.ZoneIds = zoneIds;
        discount.FlatRate = ClientDiscounts.Decimal(input.FlatRate);
        discount.SameCityRate = ClientDiscounts.Decimal(input.SameCityRate);
        discount.PickupFee = ClientDiscounts.Decimal(input.PickupFee);
        discount.IsActive = input.IsActive ?? true;
        discount.Cities.Clear();
        await db.SaveChangesAsync(cancellationToken);
        foreach (var cityId in cityIds) discount.Cities.Add(new ClientDiscountCity { DiscountId = discount.Id, CityId = cityId });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ClientRows.DetailAsync(db, caller, mapper, input.ClientId, cancellationToken);
    }
}
