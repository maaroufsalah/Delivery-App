using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoMapper;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Application.Parcels.Dtos;
using Delivery.Application.Parcels.Queries;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Parcels.Commands;

/// <summary>`sellerParcelUpdateSchema` : la fiche que le vendeur corrige avant ramassage.</summary>
public sealed record UpdateParcelBySellerCommand : IRequest<ParcelDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? Description { get; init => field = Zod.Trim(value); }

    public string? RecipientName { get; init => field = Zod.Trim(value); }

    public string? RecipientPhone { get; init; }

    public string? DestinationCityId { get; init; }

    public string? DestinationDistrictName { get; init => field = Zod.Trim(value); }

    public string? DeliveryAddress { get; init => field = Zod.Trim(value); }

    public double? CodAmount { get; init; }

    public bool? BlRequested { get; init; }

    public JsonElement? Quantity { get; init; }

    public string? ExpectedPayment { get; init; }
}

public sealed class UpdateParcelBySellerValidator : ZodValidator<UpdateParcelBySellerCommand>
{
    public UpdateParcelBySellerValidator()
    {
        RuleFor(x => x.Description).Required().MinLength(1, "required").MaxLength(255);
        RuleFor(x => x.RecipientName).Required().MinLength(1, "required").MaxLength(120);
        RuleFor(x => x.RecipientPhone).Required().Custom((phone, context) =>
        {
            foreach (var (path, message) in ParcelInputs.RecipientPhoneIssues(phone)) context.AddFailure(ValidationPaths.Raw + "recipientPhone" + path, message);
        });
        RuleFor(x => x.DestinationCityId).Required().MinLength(1, "required");
        RuleFor(x => x.DestinationDistrictName).MaxLength(ParcelInputs.DistrictNameMax);
        RuleFor(x => x.DeliveryAddress).Required().MinLength(1, "required").MaxLength(255);
        RuleFor(x => x.CodAmount).MadAmount();
        RuleFor(x => x.Quantity).Custom((quantity, context) =>
        {
            if (OptionalMad.IsPresent(quantity) && ParcelInputs.QuantityIssue(JsCoerce.Number(quantity)) is { } problem) context.AddFailure(ValidationPaths.Raw + "quantity", problem);
        });
        RuleFor(x => x.ExpectedPayment).OneOf(ParcelInputs.ExpectedPayments);
    }
}

internal sealed class UpdateParcelBySellerHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<UpdateParcelBySellerCommand, ParcelDetailDto>
{
    public async Task<ParcelDetailDto> Handle(UpdateParcelBySellerCommand input, CancellationToken cancellationToken)
    {
        var parcel = await SellerParcels.FindAsync(db, caller, input.Code, cancellationToken);
        if (input.BlRequested == true && !parcel.Client.IsPro) throw BusinessException.Conflict("Le BL ne se demande que pour un Client Pro.", new { reason = "client-not-pro" });
        var district = await ParcelInputs.ResolveDistrictAsync(db, input.DestinationCityId!, null, input.DestinationDistrictName, cancellationToken);
        if (!parcel.Client.IsPro && input.ExpectedPayment == "cheque") ParcelInputs.ExpectedPaymentFor(false, "cheque");
        var current = parcel.Quantity;
        var quantity = OptionalMad.IsPresent(input.Quantity) ? (int)JsCoerce.Number(input.Quantity) : current;
        var quantityChanged = quantity != current;
        var cityChanged = input.DestinationCityId != parcel.DestinationCityId;
        if (cityChanged && !await db.Cities.AnyAsync(c => c.Id == input.DestinationCityId && c.DeletedAt == null, cancellationToken))
        {
            throw BusinessException.Validation("Ville inconnue.", new { field = "destinationCityId" });
        }
        var before = Money.ToCents(parcel.DeliveryFee);
        parcel.Description = input.Description!;
        parcel.RecipientName = input.RecipientName!;
        parcel.RecipientPhone = ParcelInputs.JoinPhones(input.RecipientPhone!);
        parcel.DestinationCityId = input.DestinationCityId!;
        parcel.DestinationDistrictId = district.DistrictId;
        parcel.DestinationDistrictName = district.DistrictName;
        parcel.DeliveryAddress = input.DeliveryAddress!;
        parcel.CodAmount = Money.FromMad(input.CodAmount!.Value);
        if (input.BlRequested is { } bl) parcel.BlRequested = bl;
        if (parcel.Client.IsPro && !string.IsNullOrEmpty(input.ExpectedPayment)) parcel.ExpectedPayment = ParcelInputs.ExpectedPaymentFor(true, input.ExpectedPayment);
        long? nextFee = null;
        if (cityChanged || quantityChanged)
        {
            var quote = await ParcelPricing.QuoteAsync(db, parcel.OriginCityId, input.DestinationCityId!, Money.ToWire(parcel.DeliveryMode), caller.ClientId, cancellationToken);
            nextFee = ParcelInputs.CartonsFee(quote.Fee, quantity);
            parcel.DeliveryFee = Money.FromCents(nextFee.Value);
            parcel.Quantity = quantity;
        }
        if (quantityChanged)
        {
            var reason = $"Quantité {current} → {quantity}";
            var author = await ParcelStatusChange.AuthorNameAsync(db, caller.UserId, cancellationToken);
            var after = nextFee ?? before;
            ParcelJournal.Record(db, caller, parcel.Id, "quantity", current.ToString(CultureInfo.InvariantCulture), quantity.ToString(CultureInfo.InvariantCulture), reason, author);
            if (before != after) ParcelJournal.Record(db, caller, parcel.Id, "deliveryFee", before.ToString(CultureInfo.InvariantCulture), after.ToString(CultureInfo.InvariantCulture), reason, author);
        }
        await db.SaveChangesAsync(cancellationToken);
        return await GetParcelHandler.LoadAsync(db, caller, mapper, new GetParcelQuery(parcel.Code), cancellationToken);
    }
}
