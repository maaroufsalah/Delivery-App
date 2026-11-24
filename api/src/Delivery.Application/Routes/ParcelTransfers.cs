using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.BulkJobs;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Routes;

/// <summary>`transferParcels` : partagé par le bureau, la tâche de masse `parcel-transfer` et le scan du livreur.</summary>
public static class ParcelTransfers
{
    public static readonly int[] ClosedStatusIds = [5, 21, 22, 23, 24];

    public sealed record Options(bool ByScan = false, bool? OpenDraft = null);

    public sealed record Courier(string Id, string CityId, string FirstName, string LastName, IReadOnlyList<TransferCityDto> Cities)
    {
        public string Name => $"{FirstName} {LastName}".Trim();
    }

    public static void CheckIds(IReadOnlyList<string>? ids, Action<string, string> fail)
    {
        if (ids is null) return;
        if (ids.Count < 1) fail(ValidationPaths.Raw + "parcelIds", "required");
        if (ids.Count > NoteRules.SelectionMax) fail(ValidationPaths.Raw + "parcelIds", "too-many");
        for (var i = 0; i < ids.Count; i++) if (string.IsNullOrEmpty(ids[i])) fail(ValidationPaths.Raw + $"parcelIds.{i}", Zod.TooSmallString(1));
    }

    /// <summary>`transferCourier` : le livreur qui reçoit, actif.</summary>
    public static async Task<Courier> CourierAsync(IAppDbContext db, string courierId, CancellationToken cancellationToken)
    {
        var row = await db.Couriers.AsNoTracking().Where(c => c.Id == courierId && c.DeletedAt == null)
                      .Select(c => new { c.Id, c.CityId, c.IsActive, c.FirstName, c.LastName, Cities = c.Cities.Select(r => new TransferCityDto(r.CityId, r.City.Name)).ToList() })
                      .FirstOrDefaultAsync(cancellationToken)
                  ?? throw BusinessException.NotFound("Livreur introuvable.");
        if (!row.IsActive) throw BusinessException.Conflict("Ce livreur est désactivé : il ne reçoit plus de colis.", new { reason = "courier-inactive" });
        return new Courier(row.Id, row.CityId, row.FirstName, row.LastName, row.Cities);
    }

    private sealed record From(string Id, string Code, RouteKind Kind, RouteStatus Status, string CourierId, string CourierName);

    private sealed record Move(Parcel Parcel, string CityName, From? From, bool Reroute);

    private sealed record Target(string Id, string Code, string CityId);

    public static async Task<object> RunAsync(IAppDbContext db, ICurrentUser caller, IClock clock, ILogger logger, BulkJobQueue? jobs, TransferParcelsCommand input, Options options,
        CancellationToken cancellationToken)
    {
        var ids = input.ParcelIds!.Distinct().ToList();
        var reason = string.IsNullOrWhiteSpace(input.Reason) ? null : input.Reason.Trim();
        var courier = await CourierAsync(db, input.CourierId!, cancellationToken);
        if (courier.Cities.Count == 0)
        {
            throw BusinessException.Conflict($"{courier.Name} n’a aucune ville cochée : complétez sa fiche pour lui transférer des colis.", new { reason = "courier-no-city" });
        }
        var parcels = await db.Parcels.Include(p => p.DestinationCity).Include(p => p.DestinationDistrict).Where(p => ids.Contains(p.Id) && p.DeletedAt == null).ToListAsync(cancellationToken);
        var links = await db.RouteParcels.AsNoTracking().Where(l => ids.Contains(l.ParcelId) && l.Route.Status != RouteStatus.CLOSED)
            .Select(l => new { l.ParcelId, Route = new From(l.Route.Id, l.Route.Code, l.Route.Kind, l.Route.Status, l.Route.CourierId, (l.Route.Courier.FirstName + " " + l.Route.Courier.LastName).Trim()) })
            .ToListAsync(cancellationToken);
        var handovers = await db.ReturnHandoverItems.AsNoTracking().Where(i => ids.Contains(i.ParcelId) && i.Handover.Status != ReturnHandoverStatus.CANCELLED)
            .Select(i => new { i.ParcelId, i.Handover.Code, i.Handover.Status }).ToListAsync(cancellationToken);
        if (parcels.Count != ids.Count) throw BusinessException.Validation("Colis inconnu.", new { field = "parcelIds" });
        var served = courier.Cities.Select(c => c.Id).Append(courier.CityId).ToHashSet();
        var moves = new List<Move>();
        var skipped = 0;
        string? alreadyOn = null;
        foreach (var parcel in parcels)
        {
            var from = links.FirstOrDefault(l => l.ParcelId == parcel.Id)?.Route;
            if (from is null && (parcel.StatusId != RouteStatusIds.OutForDelivery || options.ByScan))
            {
                throw BusinessException.Conflict($"Le colis {parcel.Code} n’est sur aucun circuit ouvert : ajoutez-le depuis le circuit du livreur.", new { reason = "parcel-not-on-route", code = parcel.Code });
            }
            if (from is null && parcel.BillingState != BillingState.TO_INVOICE)
            {
                throw BusinessException.Conflict($"Le colis {parcel.Code} est déjà facturé : il ne se transfère plus.", new { reason = "parcel-invoiced", code = parcel.Code });
            }
            if (from is not null && from.CourierId == courier.Id)
            {
                skipped++;
                alreadyOn = from.Code;
                continue;
            }
            if (from is not null && from.Kind != RouteKind.DELIVERY)
            {
                throw BusinessException.Conflict($"Le colis {parcel.Code} est sur un circuit de {(from.Kind == RouteKind.PICKUP ? "ramassage" : "retour")} : seul un colis à livrer se transfère.",
                    new { reason = "transfer-delivery-only", code = parcel.Code, routeCode = from.Code });
            }
            if (parcel.StatusId == RouteStatusIds.Delivered)
            {
                throw BusinessException.Conflict($"Le colis {parcel.Code} est livré : son COD est encaissé. Annulez d’abord la livraison.", new { reason = "parcel-delivered", code = parcel.Code });
            }
            if (ClosedStatusIds.Contains(parcel.StatusId))
            {
                throw BusinessException.Conflict($"Le colis {parcel.Code} est retourné : il ne se transfère plus.", new { reason = "parcel-finished", code = parcel.Code, statusId = parcel.StatusId });
            }
            var handover = handovers.FirstOrDefault(h => h.ParcelId == parcel.Id && (options.ByScan || (h.Status != ReturnHandoverStatus.CONFIRMED && h.Status != ReturnHandoverStatus.CLOSED)));
            if (handover is not null)
            {
                throw BusinessException.Conflict($"Le colis {parcel.Code} est sur le bon de retour {handover.Code} : il suit son bon.",
                    new { reason = "parcel-on-return-handover", code = parcel.Code, handoverCode = handover.Code });
            }
            var reroute = !served.Contains(parcel.DestinationCityId)
                          || (ids.Count == 1 && !input.FromJob && input.DestinationCityId is not null && input.DestinationCityId != parcel.DestinationCityId);
            if (reroute && StatusCatalog.IsDestinationLocked(parcel.StatusId))
            {
                throw BusinessException.Validation($"Le colis {parcel.Code} est « {DocumentLabels.ParcelStatus(parcel.StatusId)} » : sa destination ne se modifie plus.",
                    new { reason = "parcel-destination-locked", code = parcel.Code, statusId = parcel.StatusId });
            }
            moves.Add(new Move(parcel, parcel.DestinationCity.Name, from, reroute));
        }
        if (moves.Count == 0) return new ParcelTransferResultDto(0, skipped, alreadyOn, false);
        if (!input.FromJob)
        {
            var sources = moves.Select(m => m.From?.Code).Distinct().ToList();
            var target = sources.Count == 1 && sources[0] is { } only ? only : $"TR-{courier.Id}";
            if (input.Async && jobs is not null && ids.Count > NoteRules.BulkAsyncThreshold)
            {
                return new BulkJobAcceptedDto(await BulkJobWrites.CreateAsync(db, caller, jobs, BulkJobKinds.ParcelTransfer, target,
                    new Dictionary<string, object?> { ["courierId"] = input.CourierId, ["reason"] = input.Reason, ["destinationCityId"] = input.DestinationCityId }, ids, cancellationToken));
            }
            await BulkJobWrites.AssertIdleAsync(db, target, cancellationToken);
        }
        var reroutes = moves.Where(m => m.Reroute).ToList();
        TransferCityDto? rerouteCity = null;
        if (reroutes.Count > 0)
        {
            await ParcelDestination.AssertFreeOfMoneyAsync(db, reroutes.Select(m => (m.Parcel.Id, m.Parcel.Code, m.Parcel.BillingState)).ToList(), cancellationToken);
            rerouteCity = OutForDelivery.RerouteCity(courier.Cities, courier.Name, input.DestinationCityId, reroutes.Count);
        }
        var dayStart = clock.TodayStartUtc();
        var running = await db.Routes.AsNoTracking().Where(r => r.CourierId == courier.Id && r.Kind == RouteKind.DELIVERY && (r.Status == RouteStatus.DISPATCHED || r.Status == RouteStatus.DELIVERING))
            .OrderByDescending(r => r.StatusAt).Select(r => new Target(r.Id, r.Code, r.CityId)).FirstOrDefaultAsync(cancellationToken);
        var draft = await db.Routes.AsNoTracking().Where(r => r.CourierId == courier.Id && r.Kind == RouteKind.DELIVERY && r.Status == RouteStatus.DRAFT && r.CreatedAt >= dayStart)
            .OrderByDescending(r => r.CreatedAt).Select(r => new Target(r.Id, r.Code, r.CityId)).FirstOrDefaultAsync(cancellationToken);
        bool ToDraft(Move m) => m.From?.Status == RouteStatus.DRAFT || running is null;
        var wantsDraft = moves.Any(ToDraft);
        if (wantsDraft)
        {
            foreach (var move in moves.Where(ToDraft))
            {
                if (StatusCatalog.RouteDispatchPath("delivery", move.Parcel.StatusId) is null)
                {
                    throw BusinessException.Conflict($"Le colis {move.Parcel.Code} ne peut pas repartir en livraison depuis son statut.",
                        new { reason = "parcel-not-dispatchable", code = move.Parcel.Code, statusId = move.Parcel.StatusId });
                }
            }
            if (draft is null && options.OpenDraft == false)
            {
                throw BusinessException.Conflict("La création de tournée par scan est désactivée. Demandez à votre agence d’ouvrir la tournée.", new { reason = "scan-disabled" });
            }
        }
        var routeCreated = false;
        var now = clock.UtcNow;
        var author = await ParcelStatusChange.AuthorNameAsync(db, caller.UserId, cancellationToken);
        List<(string ReturnCode, string ExchangeCode)> followed = [];
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            if (wantsDraft && draft is null)
            {
                var opened = await OutForDelivery.OpenDraftAsync(db, caller, courier.Id, courier.CityId, cancellationToken);
                draft = new Target(opened.Id, opened.Code, opened.CityId);
                routeCreated = true;
            }
            Target TargetOf(Move m) => !ToDraft(m) ? running! : draft!;
            var exchangesOnRunning = new List<Parcel>();
            foreach (var move in moves)
            {
                var parcel = move.Parcel;
                var to = TargetOf(move);
                if (move.Reroute && rerouteCity is not null)
                {
                    await ParcelDestination.ApplyAsync(db, caller, parcel, new ChangeParcelDestinationCommand
                    {
                        Code = parcel.Code, DestinationCityId = rerouteCity.Id, Reason = $"Transfert à {courier.Name}{(reason is null ? string.Empty : $" — {reason}")}",
                    }, new ParcelDestination.Transfer($"Destination changée : {move.CityName} → {rerouteCity.Name} (transfert à {courier.Name})", null), cancellationToken);
                    await db.SaveChangesAsync(cancellationToken);
                }
                if (move.From is { } source) await db.RouteParcels.Where(l => l.RouteId == source.Id && l.ParcelId == parcel.Id).ExecuteDeleteAsync(cancellationToken);
                var last = await db.RouteParcels.Where(l => l.RouteId == to.Id).MaxAsync(l => (int?)l.Sequence, cancellationToken) ?? 0;
                db.RouteParcels.Add(new RouteParcel { RouteId = to.Id, ParcelId = parcel.Id, Sequence = last + 1 });
                var previousCourier = parcel.CourierId;
                if (move.From is null || move.From.Status != RouteStatus.DRAFT) parcel.CourierId = courier.Id;
                var summary = (move.From is { } f
                                  ? $"Transféré de {f.CourierName} ({f.Code}) à {courier.Name} ({to.Code})"
                                  : $"Transféré à {courier.Name} ({to.Code}) — colis « En cours de livraison » hors circuit")
                              + (options.ByScan ? " — scan du livreur" : string.Empty) + (reason is null ? string.Empty : $" — {reason}");
                db.ParcelEvents.Add(new ParcelEvent { ParcelId = parcel.Id, StatusId = parcel.StatusId, Comment = summary, AuthorId = caller.UserId, AuthorName = author, CityId = to.CityId, CreatedAt = now });
                db.AuditLogs.Add(new AuditLog
                {
                    Entity = "Parcel", EntityId = parcel.Id, Action = AuditAction.UPDATE,
                    Before = JsonSerializer.Serialize(move.From is { } b ? new { routeCode = (string?)b.Code, courierId = (string?)b.CourierId } : new { routeCode = (string?)null, courierId = previousCourier }),
                    After = JsonSerializer.Serialize(new { routeCode = to.Code, courierId = courier.Id, transferred = true, summary }), Diff = ["courierId"], UserId = caller.UserId, At = now,
                });
                await db.SaveChangesAsync(cancellationToken);
                if (running is not null && to.Id == running.Id) exchangesOnRunning.Add(parcel);
            }
            if (running is not null) followed = await RouteDispatch.FollowExchangeReturnsAsync(db, caller, running.Code, courier.Id, running.CityId, exchangesOnRunning, author, now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        var routeCode = (!ToDraft(moves[0]) ? running! : draft!).Code;
        await SettleAsync(db, caller, logger, courier, moves, routeCode, rerouteCity, reroutes, options, followed, cancellationToken);
        return new ParcelTransferResultDto(moves.Count, skipped, routeCode, routeCreated) { Rerouted = rerouteCity is null ? null : reroutes.Count, ReroutedCityName = rerouteCity?.Name };
    }

    private static async Task SettleAsync(IAppDbContext db, ICurrentUser caller, ILogger logger, Courier courier, IReadOnlyList<Move> moves, string routeCode, TransferCityDto? rerouteCity,
        IReadOnlyList<Move> reroutes, Options options, IReadOnlyList<(string ReturnCode, string ExchangeCode)> followed, CancellationToken cancellationToken)
    {
        foreach (var source in moves.Where(m => m.From is not null).Select(m => m.From!).DistinctBy(f => f.Id).Where(f => f.Status != RouteStatus.DRAFT))
        {
            await RouteClosing.AutoCloseDeliveryAsync(db, caller, logger, source.Id, cancellationToken);
        }
        foreach (var source in moves.Where(m => m.From is not null).GroupBy(m => m.From!.Id))
        {
            var first = source.First();
            await RouteDispatch.NotifyAsync(db, logger, first.From!.CourierId, "parcel-transferred-out",
                new Dictionary<string, object> { ["count"] = source.Count(), ["code"] = first.Parcel.Code, ["courier"] = courier.Name }, new Dictionary<string, string> { ["routeCode"] = first.From.Code },
                cancellationToken);
        }
        if (!options.ByScan)
        {
            await RouteDispatch.NotifyAsync(db, logger, courier.Id, "parcel-transferred-in", new Dictionary<string, object> { ["count"] = moves.Count, ["code"] = moves[0].Parcel.Code, ["route"] = routeCode },
                new Dictionary<string, string> { ["routeCode"] = routeCode }, cancellationToken);
        }
        foreach (var (returnCode, exchangeCode) in followed)
        {
            await RouteDispatch.NotifyAsync(db, logger, courier.Id, "return-assigned", new Dictionary<string, object> { ["code"] = returnCode, ["exchange"] = exchangeCode },
                new Dictionary<string, string> { ["parcelCode"] = returnCode }, cancellationToken);
        }
        if (rerouteCity is null) return;
        foreach (var move in reroutes)
        {
            try
            {
                await Notifier.NotifyMerchantAsync(db, move.Parcel.ClientId, "parcel-rerouted", new Dictionary<string, object> { ["code"] = move.Parcel.Code, ["city"] = rerouteCity.Name },
                    new Dictionary<string, string> { ["parcelCode"] = move.Parcel.Code }, cancellationToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogWarning("Notification « parcel-rerouted » de {Code} non envoyée : {Message}", move.Parcel.Code, error.Message);
            }
            if (!options.ByScan)
            {
                await RouteDispatch.NotifyAsync(db, logger, courier.Id, "parcel-destination-changed", new Dictionary<string, object> { ["code"] = move.Parcel.Code },
                    new Dictionary<string, string> { ["topic"] = "parcel-destination-changed", ["parcelCode"] = move.Parcel.Code }, cancellationToken);
            }
        }
    }
}
