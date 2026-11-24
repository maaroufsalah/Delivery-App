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

/// <summary>`POST /parcels/:code/exchange` (`parcelExchangeSchema`) : retour RET- et échange ECH- d'un colis livré.</summary>
public sealed record RequestParcelExchangeCommand : IRequest<ExchangeDetailDto>
{
    public static readonly string[] ReturnTargets = ["agency", "home"];
    public static readonly string[] ExchangePickups = ["initial", "return-address"];

    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? ReturnTarget { get; init; }

    public string? ReturnAgencyId { get; init => field = Zod.Trim(value); }

    public string? ExchangePickup { get; init; }

    public double? Price { get; init; } = 0;

    public bool? AlreadyPaid { get; init; } = false;

    public bool? FeeIncluded { get; init; } = true;

    public string? ReturnedProduct { get; init => field = Zod.Trim(value); }

    public string? NewProduct { get; init => field = Zod.Trim(value); }

    public string? Note { get; init => field = Zod.Trim(value); }
}

public sealed class RequestParcelExchangeValidator : ZodValidator<RequestParcelExchangeCommand>
{
    public RequestParcelExchangeValidator()
    {
        RuleFor(x => x.ReturnTarget).Required().OneOf(RequestParcelExchangeCommand.ReturnTargets);
        RuleFor(x => x.ExchangePickup).Required().OneOf(RequestParcelExchangeCommand.ExchangePickups);
        RuleFor(x => x.Price).Int().Min(0);
        RuleFor(x => x.ReturnedProduct).MaxLength(255);
        RuleFor(x => x.NewProduct).MaxLength(255);
        RuleFor(x => x.Note).MaxLength(500);
    }

    protected override void Refine(RequestParcelExchangeCommand value, Action<string, string> issue)
    {
        if (value.ReturnTarget == "agency" && string.IsNullOrEmpty(value.ReturnAgencyId)) issue(ValidationPaths.Raw + "returnAgencyId", "required");
        if (value.AlreadyPaid == true && value.Price > 0) issue(ValidationPaths.Raw + "price", "paid-with-price");
    }
}

internal sealed class RequestParcelExchangeHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper, IClock clock) : IRequestHandler<RequestParcelExchangeCommand, ExchangeDetailDto>
{
    public async Task<ExchangeDetailDto> Handle(RequestParcelExchangeCommand input, CancellationToken cancellationToken)
    {
        var original = await ParcelFilters.Scoped(db.Parcels.AsNoTracking(), caller).Include(p => p.Client).Include(p => p.DestinationCity)
                           .FirstOrDefaultAsync(p => p.Code == input.Code, cancellationToken)
                       ?? throw BusinessException.NotFound($"Colis introuvable : {input.Code}");
        if (original.StatusId != StatusCatalog.Delivered)
        {
            throw BusinessException.Validation($"Le colis {original.Code} n'est pas livré : il n'y a rien à échanger.",
                new { reason = "parcel-not-delivered", code = original.Code, statusId = original.StatusId });
        }
        var existing = await ExchangeRows.Scoped(db.Exchanges.AsNoTracking(), caller)
            .Where(x => x.OriginalParcelId == original.Id && x.Status != ExchangeStatus.ANNULE).Select(x => x.Code).FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            throw BusinessException.Conflict($"Le colis {original.Code} a déjà une demande en cours : {existing}.", new { reason = "exchange-already-open", code = existing });
        }
        var agency = input.ReturnTarget == "agency"
            ? await db.Agencies.AsNoTracking().FirstOrDefaultAsync(a => a.Id == input.ReturnAgencyId && a.DeletedAt == null, cancellationToken)
              ?? throw BusinessException.NotFound("Agence de retrait introuvable.")
            : null;
        var returnCityId = agency?.CityId ?? original.OriginCityId;
        var returnAddress = agency is not null
            ? $"{agency.Name}{(string.IsNullOrEmpty(agency.Address) ? string.Empty : $", {agency.Address}")}"
            : original.Client.Address ?? original.PickupAddress;
        var policy = await db.PricingPolicyAsync(cancellationToken, original.AgencyId);
        var returnFee = original.OriginCityId == returnCityId ? policy.FailureFeeSameCity : policy.FailureFeeCrossCity;
        var now = clock.UtcNow;
        var price = (long)(input.Price ?? 0);
        var exchange = await ExchangeWrites.OpenAsync(db, caller, original,
            string.IsNullOrEmpty(input.ReturnedProduct) ? original.Description : input.ReturnedProduct,
            string.IsNullOrEmpty(input.NewProduct) ? original.Description : input.NewProduct,
            returnAddress, price, returnFee, null, input.Note,
            input.ReturnTarget == "agency" ? $"Retour à récupérer à l'agence {agency?.Name ?? string.Empty}".Trim() : "Retour à domicile", now, cancellationToken);

        var returnParcel = await ParcelCreation.CreateAsync(db, caller, mapper, clock, new CreateParcelFields
        {
            ClientId = original.ClientId,
            AgencyId = original.AgencyId,
            ExternalCode = string.Empty,
            Description = string.IsNullOrEmpty(input.ReturnedProduct) ? original.Description : input.ReturnedProduct,
            RecipientName = original.RecipientName,
            RecipientPhone = original.RecipientPhone,
            OriginCityId = original.DestinationCityId,
            PickupAddress = original.DeliveryAddress,
            PickupAtSender = true,
            DestinationCityId = returnCityId,
            DeliveryTarget = input.ReturnTarget == "agency" ? "agency" : "district",
            DeliveryAddress = returnAddress,
            CodAmount = 0,
            AlreadyPaid = true,
            Fragile = original.Fragile,
        }, new DerivedParcel($"{ParcelAdmin.ReturnPrefix}-{original.Code}", StatusCatalog.ReturnPending, ParcelKind.RETURN, original.Id, returnFee, returnFee), null, cancellationToken);

        var fromReturnAddress = input.ExchangePickup == "return-address";
        var exchangeParcel = await ParcelCreation.CreateAsync(db, caller, mapper, clock, new CreateParcelFields
        {
            ClientId = original.ClientId,
            AgencyId = original.AgencyId,
            ExternalCode = string.Empty,
            Description = string.IsNullOrEmpty(input.NewProduct) ? original.Description : input.NewProduct,
            RecipientName = original.RecipientName,
            RecipientPhone = original.RecipientPhone,
            OriginCityId = fromReturnAddress ? returnCityId : original.OriginCityId,
            PickupAddress = fromReturnAddress ? returnAddress : original.PickupAddress,
            PickupAtSender = true,
            DestinationCityId = original.DestinationCityId,
            DestinationDistrictId = original.DestinationDistrictId,
            DestinationDistrictName = original.DestinationDistrictName,
            DeliveryTarget = Money.ToWire(original.DeliveryMode),
            DeliveryAddress = original.DeliveryAddress,
            CodAmount = price / 100.0,
            AlreadyPaid = input.AlreadyPaid ?? false,
            OpenOnDelivery = original.OpenOnDelivery,
            Fragile = original.Fragile,
        }, new DerivedParcel($"{ParcelAdmin.ExchangePrefix}-{original.Code}", StatusCatalog.AwaitingArrival, ParcelKind.EXCHANGE, original.Id), null, cancellationToken);

        var author = await ExchangeRows.AuthorNameAsync(db, caller.UserId, cancellationToken);
        var tracked = await db.Exchanges.FirstAsync(x => x.Id == exchange.Id, cancellationToken);
        tracked.Status = ExchangeStatus.NOUVEAU_COLIS_CREE;
        tracked.NewParcelId = exchangeParcel.Id;
        db.ExchangeEvents.Add(new ExchangeEvent
        {
            ExchangeId = exchange.Id, Status = ExchangeStatus.NOUVEAU_COLIS_CREE,
            Comment = $"Colis {returnParcel.Code} et {exchangeParcel.Code} créés", AuthorId = caller.UserId, AuthorName = author,
        });
        ParcelJournal.Record(db, caller, original.Id, "exchange", null, exchange.Code, $"Demande de retour/échange : {returnParcel.Code} et {exchangeParcel.Code}", author);
        ParcelJournal.Record(db, caller, returnParcel.Id, "exchange", original.Code, exchange.Code, $"Retour du colis {original.Code}", author);
        ParcelJournal.Record(db, caller, exchangeParcel.Id, "exchange", original.Code, exchange.Code, $"Échange du colis {original.Code}", author);
        await db.SaveChangesAsync(cancellationToken);
        return await ExchangeRows.DetailAsync(db, caller, mapper, exchange.Code, cancellationToken);
    }
}
