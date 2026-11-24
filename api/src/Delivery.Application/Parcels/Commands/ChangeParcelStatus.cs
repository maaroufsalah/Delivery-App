using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using Delivery.Domain.Pricing;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Parcels.Commands;

/// <summary>`POST /parcels/:code/events` : un nouveau statut (`changeStatusBodySchema`).</summary>
public sealed record ChangeParcelStatusCommand : IRequest<ParcelEventDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public JsonElement? StatusId { get; init; }

    public string? Comment { get; init => field = Zod.Trim(value); }

    public string? ScheduledDate { get; init; }

    public bool? Dispute { get; init; }

    public string? CourierId { get; init => field = Zod.Trim(value); }

    public string? DestinationCityId { get; init => field = Zod.Trim(value); }

    [JsonIgnore]
    public int StatusIdValue => (int)JsCoerce.Number(StatusId);
}

public sealed class ChangeParcelStatusValidator : ZodValidator<ChangeParcelStatusCommand>
{
    public ChangeParcelStatusValidator()
    {
        RuleFor(x => x.StatusId).Custom((value, context) =>
        {
            var number = JsCoerce.Number(value);
            var issue = double.IsNaN(number) ? "Invalid input: expected number, received NaN"
                : Math.Floor(number) != number || double.IsInfinity(number) ? Zod.ExpectedInt
                : number <= 0 ? "Too small: expected number to be >0"
                : null;
            if (issue is not null) context.AddFailure(Common.Behaviors.ValidationPaths.Raw + "statusId", issue);
        });
        RuleFor(x => x.Comment).MaxLength(500);
        RuleFor(x => x.CourierId).MaxLength(64);
        RuleFor(x => x.DestinationCityId).MaxLength(64);
    }
}

internal sealed class ChangeParcelStatusHandler(IAppDbContext db, ICurrentUser caller, IClock clock, ILogger<ChangeParcelStatusHandler> logger) : IRequestHandler<ChangeParcelStatusCommand, ParcelEventDto>
{
    public Task<ParcelEventDto> Handle(ChangeParcelStatusCommand request, CancellationToken cancellationToken) =>
        ParcelStatusChange.ChangeAsync(db, caller, clock, logger, request.Code, new StatusChange(request.StatusIdValue, request.Comment, request.ScheduledDate, request.Dispute, request.CourierId, request.DestinationCityId), false, cancellationToken);
}

public sealed record StatusChange(int StatusId, string? Comment = null, string? ScheduledDate = null, bool? Dispute = null, string? CourierId = null, string? DestinationCityId = null);

/// <summary>`ParcelsService.changeStatus` et ses gardes (terminal, contestation, argent, circuit).</summary>
public static class ParcelStatusChange
{
    public static async Task<ParcelEventDto> ChangeAsync(IAppDbContext db, ICurrentUser caller, IClock clock, ILogger logger, string code, StatusChange input, bool adminFieldRevision, CancellationToken cancellationToken)
    {
        var parcel = await ParcelFilters.Scoped(db.Parcels.AsNoTracking(), caller).Where(p => p.Code == code)
            .Select(p => new { p.Id, p.Code, p.StatusId, p.BillingState, p.AgencyId, p.CurrentCityId, p.OriginCityId, p.DestinationCityId, p.CourierId, p.IsFailed, p.FailureFee, p.ClientId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw BusinessException.NotFound($"Colis introuvable : {code}");
        var target = StatusCatalog.Find(input.StatusId) ?? throw BusinessException.Validation($"Statut inconnu : {input.StatusId}");

        var outForDelivery = input.StatusId == StatusCatalog.OutForDelivery;
        if (outForDelivery)
        {
            if (parcel.StatusId == StatusCatalog.Delivered)
            {
                throw BusinessException.Conflict("Ce colis est livré : il revient en livraison par « Annuler la livraison », qui rouvre son circuit et défait l’encaissement.", new { reason = "cancel-delivery-instead" });
            }
            if (string.IsNullOrWhiteSpace(input.CourierId))
            {
                throw BusinessException.Validation("« En cours de livraison » demande un livreur : le colis entre dans son circuit de livraison.", new { reason = "courier-required", field = "courierId" });
            }
        }

        await AssertFreeOfMoneyAsync(db, parcel.Id, parcel.BillingState, input, cancellationToken);

        var locked = StatusCatalog.IsLocked(parcel.StatusId);
        if (input.Dispute == true)
        {
            if (!locked) throw BusinessException.Validation("Seul un statut terminal se conteste.");
            if (string.IsNullOrWhiteSpace(input.Comment)) throw BusinessException.Validation("Une contestation exige un motif.");
            if (!caller.HasRole(RoleName.SUPER_ADMIN) && !caller.HasRole(RoleName.STAFF)) throw BusinessException.Forbidden("Seul le staff peut contester un statut terminal.");
            var allowed = StatusCatalog.DisputeTransitions(parcel.StatusId);
            if (!allowed.Contains(input.StatusId))
            {
                throw BusinessException.Transition($"Une contestation ne mène pas de {parcel.StatusId} à {input.StatusId}.",
                    new { reason = "dispute-target-forbidden", from = parcel.StatusId, to = input.StatusId, allowed });
            }
        }
        else if (locked)
        {
            throw BusinessException.Forbidden("Ce colis est dans un statut terminal. Utilisez la contestation.", new { statusId = parcel.StatusId });
        }
        else if (!StatusCatalog.CanTransition(parcel.StatusId, input.StatusId)
                 && !(adminFieldRevision && caller.IsSuperAdmin() && StatusCatalog.CanAdminReviseField(parcel.StatusId, input.StatusId)))
        {
            throw BusinessException.Transition($"Transition interdite : {parcel.StatusId} → {input.StatusId}", new { from = parcel.StatusId, to = input.StatusId });
        }

        var now = DateTime.UtcNow;
        var sameCity = parcel.OriginCityId == parcel.DestinationCityId;
        var policy = await db.PricingPolicyAsync(cancellationToken, parcel.AgencyId);
        var failure = FailureFees.AfterTransition(parcel.IsFailed, Money.ToCentsOrNull(parcel.FailureFee), input.StatusId,
            target.Group is "failure" or "retry", FailureFees.For(input.StatusId, sameCity, policy));
        var author = await AuthorNameAsync(db, caller.UserId, cancellationToken);
        var placement = outForDelivery
            ? await Routes.OutForDelivery.PrepareAsync(db, caller, clock, parcel.Id, parcel.Code, parcel.DestinationCityId, parcel.CourierId, parcel.BillingState, input.CourierId!.Trim(),
                string.IsNullOrWhiteSpace(input.DestinationCityId) ? null : input.DestinationCityId.Trim(), cancellationToken)
            : null;

        var scheduled = ParcelCreation.ParseDate(input.ScheduledDate);
        var evt = new ParcelEvent
        {
            ParcelId = parcel.Id,
            StatusId = input.StatusId,
            Comment = string.IsNullOrEmpty(input.Comment) ? null : input.Comment,
            AuthorId = caller.UserId,
            AuthorName = author,
            CityId = parcel.CurrentCityId,
            ScheduledDate = scheduled,
            IsDispute = input.Dispute == true,
            CreatedAt = now,
        };
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var tracked = await db.Parcels.FirstOrDefaultAsync(p => p.Id == parcel.Id && p.StatusId == parcel.StatusId && p.DeletedAt == null, cancellationToken)
                ?? throw BusinessException.Conflict("Le statut de ce colis vient de changer : rechargez la fiche avant de recommencer.", new { reason = "parcel-status-changed", from = parcel.StatusId });
            tracked.StatusId = input.StatusId;
            tracked.StatusAt = now;
            tracked.IsFailed = failure.IsFailed;
            if (!failure.Keep) tracked.FailureFee = failure.FailureFee is { } fee ? Money.FromCents(fee) : null;
            if (scheduled is not null) tracked.ScheduledAt = scheduled;
            db.ParcelEvents.Add(evt);
            await db.SaveChangesAsync(cancellationToken);
            if (placement is not null) await Routes.OutForDelivery.PlaceAsync(db, caller, placement, now, author, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        if (placement is not null) await Routes.OutForDelivery.SettleAsync(db, caller, logger, placement, cancellationToken);
        await NotifyMerchantOfStatusAsync(db, logger, parcel.ClientId, code, input.StatusId, input.ScheduledDate, cancellationToken);
        return new ParcelEventDto(evt.Id, evt.ParcelId, evt.StatusId, evt.Comment, evt.AuthorName, evt.CityId, evt.AttachmentName, evt.ScheduledDate, evt.CreatedAt);
    }

    /// <summary>`assertStatusFreeOfMoney` : ni facturé, ni sur un retour de fonds, ni sur un circuit parti.</summary>
    private static async Task AssertFreeOfMoneyAsync(IAppDbContext db, string parcelId, BillingState billingState, StatusChange input, CancellationToken cancellationToken)
    {
        if (billingState != BillingState.TO_INVOICE)
        {
            throw BusinessException.Conflict("Ce colis est déjà facturé : son statut ne change plus (passez par un avoir).", new { reason = "parcel-invoiced" });
        }
        if (input.Dispute == true)
        {
            var held = await db.CashRemittanceParcels.AsNoTracking()
                .Where(r => r.ParcelId == parcelId && r.Remittance.Status != CashRemittanceStatus.CANCELLED)
                .Select(r => new { r.Remittance.Code, r.Remittance.Status }).FirstOrDefaultAsync(cancellationToken);
            if (held is not null)
            {
                throw BusinessException.Conflict(held.Status == CashRemittanceStatus.DRAFT
                        ? $"Ce colis est sur le retour de fonds {held.Code} : retirez-le du bon avant de contester."
                        : $"Ce colis a été remis par le livreur ({held.Code}) : sa contestation se règle en caisse.",
                    new { reason = "parcel-in-remittance", remittanceCode = held.Code });
            }
            return;
        }
        var onRoute = await db.RouteParcels.AsNoTracking()
            .Where(l => l.ParcelId == parcelId && l.Route.Status != RouteStatus.DRAFT && l.Route.Status != RouteStatus.CLOSED)
            .Select(l => l.Route.Code).FirstOrDefaultAsync(cancellationToken);
        if (onRoute is not null)
        {
            throw BusinessException.Conflict($"Ce colis est sur le circuit {onRoute}, parti : posez son issue depuis le circuit.", new { reason = "parcel-on-route", routeCode = onRoute });
        }
        if (input.StatusId == StatusCatalog.Delivered)
        {
            throw BusinessException.Conflict("« Livré » se pose depuis le circuit du colis : c’est lui qui encaisse et remet les fonds.", new { reason = "deliver-through-route" });
        }
    }

    /// <summary>`authorName` : prénom et nom de l'utilisateur, « Système » à défaut.</summary>
    public static async Task<string> AuthorNameAsync(IAppDbContext db, string userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.FirstName, u.LastName }).FirstOrDefaultAsync(cancellationToken);
        return user is null ? "Système" : $"{user.FirstName} {user.LastName}".Trim();
    }

    /// <summary>`notifyMerchantOfParcelStatus` : livré, reporté ou échoué ; un échec d'envoi n'annule rien.</summary>
    public static async Task NotifyMerchantOfStatusAsync(IAppDbContext db, ILogger logger, string clientId, string code, int statusId, string? scheduledDate, CancellationToken cancellationToken)
    {
        var group = StatusCatalog.GroupOf(statusId);
        var topic = statusId == StatusCatalog.Delivered ? "parcel-delivered"
            : group == "retry" && !string.IsNullOrEmpty(scheduledDate) ? "parcel-postponed"
            : group == "failure" ? "parcel-failed"
            : null;
        if (topic is null) return;
        try
        {
            var parameters = new Dictionary<string, object> { ["code"] = code };
            if (!string.IsNullOrEmpty(scheduledDate)) parameters["date"] = scheduledDate.Length > 10 ? scheduledDate[..10] : scheduledDate;
            await Notifier.NotifyMerchantAsync(db, clientId, topic, parameters, new Dictionary<string, string> { ["parcelCode"] = code }, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("Notification « {Topic} » du colis {Code} non envoyée : {Message}", topic, code, error.Message);
        }
    }
}
