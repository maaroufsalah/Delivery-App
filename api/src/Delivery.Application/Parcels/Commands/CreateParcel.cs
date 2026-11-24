using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoMapper;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Persistence;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.Parcels.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Parcels.Commands;

/// <summary>`createParcelSchema` une fois transformé (rognages, défauts).</summary>
public record CreateParcelFields
{
    public string? ClientId { get; init; }

    public string? AgencyId { get; init; }

    public string? ExternalCode { get; init => field = Zod.Trim(value); }

    public string? Description { get; init => field = Zod.Trim(value); }

    public string? RecipientName { get; init => field = Zod.Trim(value); }

    public string? RecipientPhone { get; init; }

    public string? OriginCityId { get; init; }

    public string? PickupAddress { get; init => field = Zod.Trim(value); }

    public bool? PickupAtSender { get; init; } = true;

    public string? DestinationCityId { get; init; }

    public string? DestinationDistrictId { get; init; }

    public string? DestinationDistrictName { get; init => field = Zod.Trim(value); }

    public string? DeliveryTarget { get; init; } = "district";

    public string? DeliveryAddress { get; init => field = Zod.Trim(value); }

    public double? CodAmount { get; init; }

    public bool? FeeIncluded { get; init; } = true;

    public bool? AlreadyPaid { get; init; } = false;

    public bool? OpenOnDelivery { get; init; } = false;

    public bool? Fragile { get; init; } = false;

    public bool? BlRequested { get; init; } = false;

    public JsonElement? Quantity { get; init; }

    public string? ExpectedPayment { get; init; }

    public string? ScheduledAt { get; init; }

    [JsonIgnore]
    public int QuantityValue => Quantity is null || Quantity.Value.ValueKind == JsonValueKind.Undefined ? 1 : (int)JsCoerce.Number(Quantity);
}

public sealed record CreateParcelCommand : CreateParcelFields, IRequest<ParcelDto>
{
    private static readonly System.Text.RegularExpressions.Regex IdempotencyPattern = new(@"^[\x21-\x7e]{8,80}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>En-tête `idempotency-key` : 8 à 80 caractères imprimables, ignoré sinon.</summary>
    [JsonIgnore]
    public string? IdempotencyKey { get; init => field = value is not null && IdempotencyPattern.IsMatch(value) ? value : null; }
}

public abstract class CreateParcelFieldsValidator<T> : ZodValidator<T>
    where T : CreateParcelFields
{
    protected CreateParcelFieldsValidator()
    {
        RuleFor(x => x.ClientId).Required().MinLength(1, "required");
        RuleFor(x => x.AgencyId).Must(v => v is null || v.Length >= 1).WithMessage(Zod.InvalidInput);
        RuleFor(x => x.ExternalCode).MaxLength(64);
        RuleFor(x => x.Description).Required().MinLength(1, "required").MaxLength(255);
        RuleFor(x => x.RecipientName).Required().MinLength(1, "required").MaxLength(120);
        RuleFor(x => x.RecipientPhone).Required().Custom((phone, context) =>
        {
            foreach (var (path, message) in ParcelInputs.RecipientPhoneIssues(phone)) context.AddFailure(ValidationPaths.Raw + "recipientPhone" + path, message);
        });
        RuleFor(x => x.OriginCityId).Required().MinLength(1, "required");
        RuleFor(x => x.PickupAddress).Required().MinLength(1, "required").MaxLength(255);
        RuleFor(x => x.DestinationCityId).Required().MinLength(1, "required");
        RuleFor(x => x.DestinationDistrictName).MaxLength(ParcelInputs.DistrictNameMax);
        RuleFor(x => x.DeliveryTarget).OneOf(ParcelInputs.DeliveryTargets);
        RuleFor(x => x.DeliveryAddress).Required().MinLength(1, "required").MaxLength(255);
        RuleFor(x => x.CodAmount).MadAmount();
        RuleFor(x => x.Quantity).Custom((quantity, context) =>
        {
            if (quantity is { ValueKind: not JsonValueKind.Undefined } value && ParcelInputs.QuantityIssue(JsCoerce.Number(value)) is { } problem)
            {
                context.AddFailure(ValidationPaths.Raw + "quantity", problem);
            }
        });
        RuleFor(x => x.ExpectedPayment).OneOf(ParcelInputs.ExpectedPayments);
    }
}

public sealed class CreateParcelValidator : CreateParcelFieldsValidator<CreateParcelCommand>;

/// <summary>Colis dérivé (retour, échange) : code, statut et frais imposés par l'appelant.</summary>
public sealed record DerivedParcel(string Code, int StatusId, ParcelKind Kind, string? ReferenceParcelId, long? DeliveryFee = null, long? FailureFee = null);

internal sealed class CreateParcelHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper, IClock clock) : IRequestHandler<CreateParcelCommand, ParcelDto>
{
    public Task<ParcelDto> Handle(CreateParcelCommand request, CancellationToken cancellationToken) =>
        ParcelCreation.CreateAsync(db, caller, mapper, clock, request, null, request.IdempotencyKey, cancellationToken);
}

/// <summary>`ParcelsService.create`, partagé par la saisie, l'import et les colis dérivés.</summary>
public static class ParcelCreation
{
    public static async Task<ParcelDto> CreateAsync(IAppDbContext db, ICurrentUser caller, IMapper mapper, IClock clock, CreateParcelFields input, DerivedParcel? derived, string? idempotencyKey, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var clientId = caller.ClientId ?? input.ClientId!;
        if (caller.ClientId is not null && input.ClientId != caller.ClientId) throw BusinessException.Forbidden("Création impossible pour un autre expéditeur.");
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            var already = await ByIdempotencyKeyAsync(db, caller, mapper, clientId, idempotencyKey, cancellationToken);
            if (already is not null) return already;
        }

        var client = await db.Clients.AsNoTracking().Where(c => c.Id == clientId && c.DeletedAt == null)
            .Select(c => new { c.Id, c.AgencyId, c.IsPro }).FirstOrDefaultAsync(cancellationToken)
            ?? throw BusinessException.NotFound("E-commerçant introuvable.");
        if (input.BlRequested == true && !client.IsPro) throw BusinessException.Conflict("Le BL ne se demande que pour un Client Pro.", new { reason = "client-not-pro" });
        var expectedPayment = ParcelInputs.ExpectedPaymentFor(client.IsPro, input.ExpectedPayment);
        var quantity = derived is not null ? 1 : input.QuantityValue;
        var district = await ParcelInputs.ResolveDistrictAsync(db, input.DestinationCityId!, input.DestinationDistrictId, input.DestinationDistrictName, cancellationToken);

        var chosenAgencyId = string.IsNullOrEmpty(input.AgencyId) ? null : input.AgencyId;
        if (chosenAgencyId is not null && !await db.Agencies.AnyAsync(a => a.Id == chosenAgencyId && a.DeletedAt == null, cancellationToken))
        {
            throw BusinessException.Validation("Agence inconnue.", new { field = "agencyId" });
        }
        var agencyId = chosenAgencyId ?? client.AgencyId ?? await db.AgencyForCityAsync(input.OriginCityId, cancellationToken)
                       ?? throw BusinessException.Validation("Aucune agence disponible.");
        var quote = await ParcelPricing.QuoteAsync(db, input.OriginCityId!, input.DestinationCityId!, input.DeliveryTarget ?? "district", clientId, cancellationToken);
        var statusId = derived?.StatusId ?? (input.PickupAtSender == false ? 2 : 1);
        await ParcelLookup.AssertExternalCodeFreeAsync(db.Parcels, clientId, input.ExternalCode, null, cancellationToken);

        string parcelId;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var code = derived?.Code ?? await ParcelCodes.ReserveAsync(db, input.OriginCityId!, input.DestinationCityId!, now, cancellationToken);
            var parcel = new Parcel
            {
                Code = code,
                ExternalCode = string.IsNullOrEmpty(input.ExternalCode) ? null : input.ExternalCode,
                IdempotencyKey = string.IsNullOrEmpty(idempotencyKey) ? null : idempotencyKey,
                ClientId = clientId,
                AgencyId = agencyId,
                StatusId = statusId,
                StatusAt = now,
                OriginCityId = input.OriginCityId!,
                DestinationCityId = input.DestinationCityId!,
                DestinationDistrictId = district.DistrictId,
                DestinationDistrictName = district.DistrictName,
                DeliveryMode = DeliveryMode.DISTRICT,
                CurrentCityId = input.OriginCityId!,
                Description = input.Description!,
                RecipientName = input.RecipientName!,
                RecipientPhone = ParcelInputs.JoinPhones(input.RecipientPhone!),
                PickupAddress = input.PickupAddress!,
                DeliveryAddress = input.DeliveryAddress!,
                Kind = derived?.Kind ?? ParcelKind.STANDARD,
                ReferenceParcelId = derived?.ReferenceParcelId,
                CodAmount = Money.FromMad(input.CodAmount ?? 0),
                DeliveryFee = Money.FromCents(derived?.DeliveryFee ?? ParcelInputs.CartonsFee(quote.Fee, quantity)),
                Quantity = quantity,
                FeeIncluded = true,
                AlreadyPaid = input.AlreadyPaid ?? false,
                OpenOnDelivery = input.OpenOnDelivery ?? false,
                Fragile = input.Fragile ?? false,
                BlRequested = input.BlRequested ?? false,
                ExpectedPayment = expectedPayment,
                ScheduledAt = ParseDate(input.ScheduledAt),
            };
            if (derived?.FailureFee is { } failureFee)
            {
                parcel.FailureFee = Money.FromCents(failureFee);
                parcel.IsFailed = true;
            }
            db.Parcels.Add(parcel);
            db.ParcelEvents.Add(new ParcelEvent
            {
                ParcelId = parcel.Id,
                StatusId = statusId,
                AuthorId = caller.UserId,
                AuthorName = "Système",
                CityId = input.OriginCityId,
                CreatedAt = now,
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            parcelId = parcel.Id;
        }
        catch (DbUpdateException error) when (DbErrors.IsUniqueViolation(error))
        {
            db.ChangeTracker.Clear();
            if (!string.IsNullOrEmpty(idempotencyKey) && DbErrors.IsUniqueViolation(error, "idempotencyKey"))
            {
                var winner = await ByIdempotencyKeyAsync(db, caller, mapper, clientId, idempotencyKey, cancellationToken);
                if (winner is not null) return winner;
            }
            if (DbErrors.IsUniqueViolation(error, "externalCode")) throw ParcelLookup.ExternalCodeTaken(input.ExternalCode ?? string.Empty);
            throw;
        }
        var row = await mapper.ProjectTo<ParcelRow>(db.Parcels.AsNoTracking().Where(p => p.Id == parcelId)).FirstAsync(cancellationToken);
        return ParcelViews.ToWire(row, caller);
    }

    private static async Task<ParcelDto?> ByIdempotencyKeyAsync(IAppDbContext db, ICurrentUser caller, IMapper mapper, string clientId, string key, CancellationToken cancellationToken)
    {
        var row = await mapper.ProjectTo<ParcelRow>(db.Parcels.AsNoTracking().Where(p => p.ClientId == clientId && p.IdempotencyKey == key)).FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : ParcelViews.ToWire(row, caller);
    }

    /// <summary>`new Date(text)` : une date illisible fait échouer Prisma (500), reproduit.</summary>
    public static DateTime? ParseDate(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var value)) return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        throw new InvalidOperationException($"Invalid Date: {text}");
    }
}
