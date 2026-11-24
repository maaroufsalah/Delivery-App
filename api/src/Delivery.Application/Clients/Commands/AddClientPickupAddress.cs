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

/// <summary>`POST /clients/:id/pickup-addresses` : la nouvelle adresse par défaut remplace l'ancienne.</summary>
public sealed record AddClientPickupAddressCommand : IRequest<ClientDetailDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string ClientId { get; init; } = string.Empty;

    public string? Label { get; init => field = Zod.Trim(value); }

    public string? Address { get; init => field = Zod.Trim(value); }

    public string? CityId { get; init; }

    public string? DistrictId { get; init; }

    public string? Phone { get; init => field = Phones.NormalizeOptional(value); }

    public bool? IsDefault { get; init; } = false;
}

public sealed class AddClientPickupAddressValidator : ZodValidator<AddClientPickupAddressCommand>
{
    public AddClientPickupAddressValidator()
    {
        RuleFor(x => x.Label).Required().MinLength(2, "too-short").MaxLength(60);
        RuleFor(x => x.Address).Required().MinLength(5, "too-short").MaxLength(255);
        RuleFor(x => x.CityId).Required().MinLength(1, "required");
        RuleFor(x => x.Phone).Must(p => string.IsNullOrEmpty(p) || Phones.Pattern.IsMatch(p)).WithMessage(Zod.InvalidInput);
    }
}

internal sealed class AddClientPickupAddressHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<AddClientPickupAddressCommand, ClientDetailDto>
{
    public async Task<ClientDetailDto> Handle(AddClientPickupAddressCommand input, CancellationToken cancellationToken)
    {
        await ClientRows.FindAsync(db, input.ClientId, cancellationToken);
        await ClientRows.AssertCityExistsAsync(db, input.CityId!, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (input.IsDefault == true)
        {
            await db.ClientPickupAddresses.Where(a => a.ClientId == input.ClientId && a.IsDefault).ExecuteUpdateAsync(a => a.SetProperty(x => x.IsDefault, false), cancellationToken);
        }
        db.ClientPickupAddresses.Add(new ClientPickupAddress
        {
            ClientId = input.ClientId,
            Label = input.Label!,
            Address = input.Address!,
            CityId = input.CityId!,
            DistrictId = Zod.EmptyToNull(input.DistrictId),
            Phone = Zod.EmptyToNull(input.Phone),
            IsDefault = input.IsDefault ?? false,
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ClientRows.DetailAsync(db, caller, mapper, input.ClientId, cancellationToken);
    }
}
