using System.Text.Json.Serialization;
using AutoMapper;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.Exchanges.Dtos;
using Delivery.Application.Parcels;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Exchanges.Commands;

/// <summary>`POST /exchanges` (`exchangeCreateSchema`) : l'agence ouvre un échange et crée le nouveau colis.</summary>
public sealed record CreateExchangeCommand : IRequest<ExchangeDetailDto>
{
    public string? OriginalParcelCode { get; init => field = Zod.Trim(value); }

    public string? ReturnedProduct { get; init => field = Zod.Trim(value); }

    public string? NewProduct { get; init => field = Zod.Trim(value); }

    public string? DestinationCityId { get; init => field = Zod.Trim(value); }

    public string? DestinationDistrictId { get; init => field = Zod.Trim(value); }

    public string? DestinationDistrictName { get; init => field = Zod.Trim(value); }

    public string? DeliveryAddress { get; init => field = Zod.Trim(value); }

    public string? RecipientName { get; init => field = Zod.Trim(value); }

    public string? RecipientPhone { get; init; }

    public double? Amount { get; init; } = 0;

    public double? Fees { get; init; } = 0;

    public string? CourierId { get; init => field = Zod.Trim(value); }

    public string? Note { get; init => field = Zod.Trim(value); }
}

public sealed class CreateExchangeValidator : ZodValidator<CreateExchangeCommand>
{
    public CreateExchangeValidator()
    {
        RuleFor(x => x.OriginalParcelCode).Required().MinLength(1, "required");
        RuleFor(x => x.ReturnedProduct).Required().MinLength(1, "required").MaxLength(255);
        RuleFor(x => x.NewProduct).Required().MinLength(1, "required").MaxLength(255);
        RuleFor(x => x.DestinationDistrictName).MaxLength(ParcelInputs.DistrictNameMax);
        RuleFor(x => x.DeliveryAddress).MaxLength(255);
        RuleFor(x => x.RecipientName).MaxLength(120);
        RuleFor(x => x.RecipientPhone).Custom((phone, context) =>
        {
            if (string.IsNullOrEmpty(phone)) return;
            foreach (var (path, message) in ParcelInputs.RecipientPhoneIssues(phone)) context.AddFailure(ValidationPaths.Raw + "recipientPhone" + path, message);
        });
        RuleFor(x => x.Amount).Int();
        RuleFor(x => x.Fees).Int().Min(0);
        RuleFor(x => x.Note).MaxLength(500);
    }
}

internal sealed class CreateExchangeHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper, IClock clock) : IRequestHandler<CreateExchangeCommand, ExchangeDetailDto>
{
    public async Task<ExchangeDetailDto> Handle(CreateExchangeCommand input, CancellationToken cancellationToken)
    {
        ExchangeRows.AssertAgency(caller, "Seule l’agence ouvre un échange.");
        var original = await ParcelFilters.Scoped(db.Parcels.AsNoTracking(), caller).Include(p => p.DestinationCity)
                           .FirstOrDefaultAsync(p => p.Code == input.OriginalParcelCode, cancellationToken)
                       ?? throw BusinessException.NotFound($"Colis introuvable : {input.OriginalParcelCode}");
        if (!string.IsNullOrEmpty(input.CourierId) && !await db.Couriers.AnyAsync(c => c.Id == input.CourierId && c.DeletedAt == null, cancellationToken))
        {
            throw BusinessException.NotFound("Livreur introuvable.");
        }
        var destinationCityId = string.IsNullOrEmpty(input.DestinationCityId) ? original.DestinationCityId : input.DestinationCityId;
        var deliveryAddress = string.IsNullOrEmpty(input.DeliveryAddress) ? original.DeliveryAddress : input.DeliveryAddress;
        var now = clock.UtcNow;
        var amount = (long)(input.Amount ?? 0);
        var exchange = await ExchangeWrites.OpenAsync(db, caller, original, input.ReturnedProduct!, input.NewProduct!, deliveryAddress, amount, (long)(input.Fees ?? 0),
            string.IsNullOrEmpty(input.CourierId) ? null : input.CourierId, input.Note, "Échange demandé", now, cancellationToken);

        var districtTyped = !string.IsNullOrEmpty(input.DestinationDistrictName) || !string.IsNullOrEmpty(input.DestinationDistrictId);
        var sameDestination = destinationCityId == original.DestinationCityId;
        // `codAmount: input.amount` : Speedy passe ici des centimes là où la création attend des MAD (COD ×100), reproduit.
        var fields = new CreateParcelFields
        {
            ClientId = original.ClientId,
            AgencyId = original.AgencyId,
            ExternalCode = string.Empty,
            Description = input.NewProduct,
            RecipientName = string.IsNullOrEmpty(input.RecipientName) ? original.RecipientName : input.RecipientName,
            RecipientPhone = string.IsNullOrEmpty(input.RecipientPhone) ? original.RecipientPhone : input.RecipientPhone,
            OriginCityId = original.OriginCityId,
            PickupAddress = original.PickupAddress,
            PickupAtSender = true,
            DestinationCityId = destinationCityId,
            DestinationDistrictId = districtTyped ? Zod.EmptyToNull(input.DestinationDistrictId) : sameDestination ? original.DestinationDistrictId : null,
            DestinationDistrictName = districtTyped ? Zod.EmptyToNull(input.DestinationDistrictName) : sameDestination ? original.DestinationDistrictName : null,
            DeliveryTarget = Money.ToWire(original.DeliveryMode),
            DeliveryAddress = deliveryAddress,
            CodAmount = amount,
            OpenOnDelivery = original.OpenOnDelivery,
            Fragile = original.Fragile,
        };
        var newParcel = await ParcelCreation.CreateAsync(db, caller, mapper, clock, fields, null, null, cancellationToken);
        var author = await ExchangeRows.AuthorNameAsync(db, caller.UserId, cancellationToken);
        var tracked = await db.Exchanges.FirstAsync(x => x.Id == exchange.Id, cancellationToken);
        tracked.Status = ExchangeStatus.NOUVEAU_COLIS_CREE;
        tracked.NewParcelId = newParcel.Id;
        db.ExchangeEvents.Add(new ExchangeEvent { ExchangeId = exchange.Id, Status = ExchangeStatus.NOUVEAU_COLIS_CREE, Comment = $"Colis {newParcel.Code} créé", AuthorId = caller.UserId, AuthorName = author });
        var created = await db.Parcels.FirstAsync(p => p.Id == newParcel.Id, cancellationToken);
        created.ReferenceParcelId = original.Id;
        ParcelJournal.Record(db, caller, original.Id, "exchange", null, exchange.Code, $"Échange : {input.ReturnedProduct} → {input.NewProduct}", author);
        ParcelJournal.Record(db, caller, newParcel.Id, "exchange", original.Code, exchange.Code, $"Échange : remplace {original.Code}", author);
        await db.SaveChangesAsync(cancellationToken);
        return await ExchangeRows.DetailAsync(db, caller, mapper, exchange.Code, cancellationToken);
    }
}
