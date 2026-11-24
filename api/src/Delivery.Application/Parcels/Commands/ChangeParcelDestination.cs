using System.Globalization;
using System.Text.Json.Serialization;
using AutoMapper;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels.Dtos;
using Delivery.Application.Parcels.Queries;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Parcels.Commands;

/// <summary>`PATCH /parcels/:code/destination` (`changeDestinationSchema`) : motif obligatoire, circuit et tarif suivent.</summary>
public sealed record ChangeParcelDestinationCommand : IRequest<ParcelDestinationResultDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? RecipientName { get; init => field = Zod.Trim(value); }

    public string? RecipientPhone { get; init; }

    public string? DeliveryAddress { get; init => field = Zod.Trim(value); }

    public string? DestinationCityId { get; init => field = Zod.Trim(value); }

    public string? DestinationDistrictId { get; init => field = Zod.Trim(value); }

    public string? DestinationDistrictName { get; init => field = Zod.Trim(value); }

    public string? Instructions { get; init => field = Zod.Trim(value); }

    public string? Reason { get; init => field = Zod.Trim(value); }
}

public sealed class ChangeParcelDestinationValidator : ZodValidator<ChangeParcelDestinationCommand>
{
    public ChangeParcelDestinationValidator()
    {
        RuleFor(x => x.RecipientName).MinLength(1, "required").MaxLength(120);
        RuleFor(x => x.RecipientPhone).Custom((phone, context) =>
        {
            foreach (var (path, message) in ParcelInputs.RecipientPhoneIssues(phone)) context.AddFailure(ValidationPaths.Raw + "recipientPhone" + path, message);
        });
        RuleFor(x => x.DeliveryAddress).MinLength(1, "required").MaxLength(255);
        RuleFor(x => x.DestinationDistrictName).MaxLength(ParcelInputs.DistrictNameMax);
        RuleFor(x => x.Instructions).MaxLength(500);
        RuleFor(x => x.Reason).Required().MinLength(1, "required").MaxLength(500);
    }

    protected override void Refine(ChangeParcelDestinationCommand value, Action<string, string> issue)
    {
        if (value.RecipientName is null && value.RecipientPhone is null && value.DeliveryAddress is null && value.DestinationCityId is null
            && value.DestinationDistrictId is null && value.DestinationDistrictName is null && value.Instructions is null)
        {
            issue(ValidationPaths.Raw + "deliveryAddress", "nothing-changed");
        }
    }
}

internal sealed class ChangeParcelDestinationHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper, ILogger<ChangeParcelDestinationHandler> logger)
    : IRequestHandler<ChangeParcelDestinationCommand, ParcelDestinationResultDto>
{
    public async Task<ParcelDestinationResultDto> Handle(ChangeParcelDestinationCommand input, CancellationToken cancellationToken)
    {
        var parcel = await ParcelFilters.Scoped(db.Parcels, caller).Include(p => p.DestinationDistrict).FirstOrDefaultAsync(p => p.Code == input.Code, cancellationToken)
                     ?? throw BusinessException.NotFound($"Colis introuvable : {input.Code}");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var outcome = await ParcelDestination.ApplyAsync(db, caller, parcel, input, null, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // La clôture automatique des circuits quittés (`replayAutoClose`) se branche avec les circuits.
        if (parcel.CourierId is { } courierId && (outcome.Dropped.Count > 0 || outcome.Linked > 0))
        {
            try
            {
                await Notifier.NotifyCourierAsync(db, courierId, "parcel-destination-changed", new Dictionary<string, object> { ["code"] = parcel.Code },
                    new Dictionary<string, string> { ["topic"] = "parcel-destination-changed", ["parcelCode"] = parcel.Code }, cancellationToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogWarning("Notification « parcel-destination-changed » du colis {Code} non envoyée : {Message}", parcel.Code, error.Message);
            }
        }
        var detail = await GetParcelHandler.LoadAsync<ParcelDestinationResultDto>(db, caller, mapper, new GetParcelQuery(input.Code), cancellationToken);
        detail.DroppedFromRoutes = outcome.Dropped.Select(d => d.RouteCode).ToList();
        detail.RepricedFrom = outcome.RepricedFrom;
        detail.RepricedTo = outcome.RepricedTo;
        return detail;
    }
}

/// <summary>`changeDestination` : le cœur partagé avec le transfert de colis entre circuits.</summary>
public static class ParcelDestination
{
    public sealed record Transfer(string Event, int? AsStatusId);

    public sealed record DroppedLink(string RouteId, string RouteCode, RouteKind Kind, RouteStatus Status);

    public sealed record Outcome(IReadOnlyList<DroppedLink> Dropped, int Linked, long? RepricedFrom, long? RepricedTo);

    /// <summary>Applique les changements au colis suivi ; l'appelant enregistre dans sa transaction.</summary>
    public static async Task<Outcome> ApplyAsync(IAppDbContext db, ICurrentUser caller, Parcel parcel, ChangeParcelDestinationCommand input, Transfer? transfer, CancellationToken cancellationToken)
    {
        var locked = transfer?.AsStatusId is { } asStatus ? StatusCatalog.IsDestinationLocked(asStatus) : StatusCatalog.IsDestinationLocked(parcel.StatusId);
        if (locked)
        {
            throw BusinessException.Validation($"Le colis {parcel.Code} est « {DocumentLabels.ParcelStatus(parcel.StatusId)} » : sa destination ne se modifie plus.",
                new { reason = "parcel-destination-locked", code = parcel.Code, statusId = parcel.StatusId });
        }
        await AssertFreeOfMoneyAsync(db, [(parcel.Id, parcel.Code, parcel.BillingState)], cancellationToken);
        if (!string.IsNullOrEmpty(input.DestinationCityId) && input.DestinationCityId != parcel.DestinationCityId
            && !await db.Cities.AnyAsync(c => c.Id == input.DestinationCityId && c.DeletedAt == null, cancellationToken))
        {
            throw BusinessException.NotFound("Ville introuvable.");
        }
        var cityChanged = !string.IsNullOrEmpty(input.DestinationCityId) && input.DestinationCityId != parcel.DestinationCityId;
        var phone = input.RecipientPhone is null ? null : ParcelInputs.JoinPhones(input.RecipientPhone);
        var changes = new List<(string Field, string? Old, string New)>();
        void Bump(string field, string? current, string? next)
        {
            if (next is null || next == current) return;
            changes.Add((field, current, next));
        }
        Bump("recipientName", parcel.RecipientName, input.RecipientName);
        Bump("recipientPhone", parcel.RecipientPhone, phone);
        Bump("deliveryAddress", parcel.DeliveryAddress, input.DeliveryAddress);
        Bump("destinationCityId", parcel.DestinationCityId, input.DestinationCityId);

        var districtTouched = input.DestinationDistrictName is not null || input.DestinationDistrictId is not null;
        if (districtTouched || cityChanged)
        {
            var currentName = ParcelViews.DistrictLabel(parcel.DestinationDistrictName, parcel.DestinationDistrict?.Name);
            var cityId = string.IsNullOrEmpty(input.DestinationCityId) ? parcel.DestinationCityId : input.DestinationCityId;
            var next = input.DestinationDistrictName is not null
                ? await ParcelInputs.ResolveDistrictAsync(db, cityId, null, input.DestinationDistrictName, cancellationToken)
                : input.DestinationDistrictId is not null
                    ? await ParcelInputs.ResolveDistrictAsync(db, cityId, input.DestinationDistrictId, null, cancellationToken)
                    : await ParcelInputs.ResolveDistrictAsync(db, cityId, null, currentName, cancellationToken);
            if ((next.DistrictName ?? string.Empty) != (currentName ?? string.Empty)) changes.Add(("destinationDistrictName", currentName, next.DistrictName ?? string.Empty));
            if (next.DistrictId != parcel.DestinationDistrictId) changes.Add(("destinationDistrictId", parcel.DestinationDistrictId, next.DistrictId ?? string.Empty));
            parcel.DestinationDistrictName = next.DistrictName;
            parcel.DestinationDistrictId = next.DistrictId;
        }
        if (input.RecipientName is not null) parcel.RecipientName = input.RecipientName;
        if (phone is not null) parcel.RecipientPhone = phone;
        if (input.DeliveryAddress is not null) parcel.DeliveryAddress = input.DeliveryAddress;
        if (!string.IsNullOrEmpty(input.DestinationCityId)) parcel.DestinationCityId = input.DestinationCityId;
        if (changes.Count == 0 && input.Instructions is null) throw BusinessException.Validation("La destination est déjà celle-ci.", new { reason = "nothing-changed" });

        long? repricedFrom = null, repricedTo = null;
        if (cityChanged)
        {
            var quote = await ParcelPricing.QuoteAsync(db, parcel.OriginCityId, input.DestinationCityId!, Money.ToWire(parcel.DeliveryMode), parcel.ClientId, cancellationToken);
            var before = Money.ToCents(parcel.DeliveryFee);
            var total = ParcelInputs.CartonsFee(quote.Fee, parcel.Quantity);
            if (total != before)
            {
                (repricedFrom, repricedTo) = (before, total);
                parcel.DeliveryFee = Money.FromCents(total);
                changes.Add(("deliveryFee", before.ToString(CultureInfo.InvariantCulture), total.ToString(CultureInfo.InvariantCulture)));
            }
        }

        var author = await ParcelStatusChange.AuthorNameAsync(db, caller.UserId, cancellationToken);
        var links = parcel.CourierId is not null && transfer is null
            ? await db.RouteParcels.Where(l => l.ParcelId == parcel.Id && !l.Confirmed && l.Route.Status != RouteStatus.CLOSED)
                .Select(l => new DroppedLink(l.RouteId, l.Route.Code, l.Route.Kind, l.Route.Status)).ToListAsync(cancellationToken)
            : [];
        var dropped = cityChanged ? links : [];
        var backToAgency = parcel.StatusId != StatusCatalog.AwaitingDelivery
                           && dropped.Any(l => l.Kind == RouteKind.DELIVERY && (l.Status != RouteStatus.DRAFT || parcel.StatusId == StatusCatalog.OutForDelivery));
        var now = DateTime.UtcNow;
        var previousStatus = parcel.StatusId;
        if (backToAgency)
        {
            parcel.StatusId = StatusCatalog.AwaitingDelivery;
            parcel.StatusAt = now;
            changes.Add(("statusId", previousStatus.ToString(CultureInfo.InvariantCulture), StatusCatalog.AwaitingDelivery.ToString(CultureInfo.InvariantCulture)));
        }

        foreach (var (field, old, value) in changes) ParcelJournal.Record(db, caller, parcel.Id, field, old, value, input.Reason, author);
        if (!string.IsNullOrEmpty(input.Instructions))
        {
            db.ParcelNotes.Add(new ParcelNote { ParcelId = parcel.Id, Body = input.Instructions, AuthorId = caller.UserId, AuthorName = author });
        }
        db.ParcelEvents.Add(new ParcelEvent
        {
            ParcelId = parcel.Id,
            StatusId = previousStatus,
            Comment = transfer?.Event ?? $"Destination modifiée : {input.Reason}",
            AuthorId = caller.UserId,
            AuthorName = author,
            CreatedAt = now,
        });
        if (dropped.Count > 0)
        {
            var routeIds = dropped.Select(d => d.RouteId).ToList();
            db.RouteParcels.RemoveRange(await db.RouteParcels.Where(l => l.ParcelId == parcel.Id && routeIds.Contains(l.RouteId)).ToListAsync(cancellationToken));
        }
        if (backToAgency)
        {
            db.ParcelEvents.Add(new ParcelEvent
            {
                ParcelId = parcel.Id,
                StatusId = StatusCatalog.AwaitingDelivery,
                Comment = $"Remis en attente de livraison : sorti de {string.Join(", ", dropped.Select(d => d.RouteCode))} par le changement de destination",
                AuthorId = caller.UserId,
                AuthorName = author,
                CreatedAt = now.AddMilliseconds(1),
            });
        }
        return new Outcome(dropped, links.Count, repricedFrom, repricedTo);
    }

    /// <summary>`assertDestinationFreeOfMoney` : ni facturé, ni remis en caisse.</summary>
    public static async Task AssertFreeOfMoneyAsync(IAppDbContext db, IReadOnlyList<(string Id, string Code, BillingState BillingState)> parcels, CancellationToken cancellationToken)
    {
        var invoiced = parcels.FirstOrDefault(p => p.BillingState != BillingState.TO_INVOICE);
        if (invoiced.Id is not null)
        {
            throw BusinessException.Conflict($"Le colis {invoiced.Code} est déjà facturé : sa destination et ses frais ne changent plus.", new { reason = "parcel-invoiced", code = invoiced.Code });
        }
        if (parcels.Count == 0) return;
        var ids = parcels.Select(p => p.Id).ToList();
        var remitted = await db.CashRemittanceParcels.AsNoTracking().Where(r => ids.Contains(r.ParcelId) && r.Remittance.Status == CashRemittanceStatus.CONFIRMED)
            .Select(r => new { r.ParcelId, r.Remittance.Code }).FirstOrDefaultAsync(cancellationToken);
        if (remitted is not null)
        {
            var code = parcels.FirstOrDefault(p => p.Id == remitted.ParcelId).Code;
            throw BusinessException.Conflict($"Le colis {code ?? string.Empty} est déjà remis en caisse ({remitted.Code}) : sa destination ne change plus.",
                new { reason = "parcel-remitted", code, remittanceCode = remitted.Code });
        }
    }
}
