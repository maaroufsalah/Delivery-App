using System.Globalization;
using System.Text.Json;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Finance;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Parcels.Commands;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using Delivery.Domain.Parcels;
using Delivery.Domain.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Routes;

public static class RouteStatusIds
{
    public const int Delivered = 4;
    public const int OutForDelivery = 13;
    public const int Postponed = 14;
    public const int ReturnPending = 20;
    public const int ReturnedToSender = 23;
}

/// <summary>`dispatchRoute` : le circuit part, ses colis passent au statut de départ et au livreur, en une transaction.</summary>
public static class RouteDispatch
{
    public sealed record Options(bool ParcelsPhase = false, bool FinishPhase = false, IReadOnlyList<string>? ChunkIds = null);

    public static async Task<int> RunAsync(IAppDbContext db, ICurrentUser caller, ILogger logger, string code, Options options, CancellationToken cancellationToken)
    {
        var route = await RouteRows.Scoped(db.Routes, caller).FirstOrDefaultAsync(r => r.Code == code, cancellationToken) ?? throw BusinessException.NotFound($"Circuit introuvable : {code}");
        if (route.Status != RouteStatus.DRAFT) throw BusinessException.Conflict("Ce circuit est déjà parti.", new { reason = "route-dispatched" });
        var links = await db.RouteParcels.Where(l => l.RouteId == route.Id).Include(l => l.Parcel).ToListAsync(cancellationToken);
        if (links.Count == 0) throw BusinessException.Validation("Un circuit sans colis ne part pas.");
        var kind = Money.ToWire(route.Kind);
        var now = DateTime.UtcNow;
        var dispatched = StatusCatalog.RouteDispatchStatusId(kind);
        var author = await ParcelStatusChange.AuthorNameAsync(db, caller.UserId, cancellationToken);
        var chunk = options.ParcelsPhase ? (options.ChunkIds ?? []).ToHashSet() : null;
        var moving = options.FinishPhase ? [] : links.Where(l => chunk is null || (chunk.Contains(l.ParcelId) && l.Parcel.StatusId != dispatched)).ToList();
        List<(string ReturnCode, string ExchangeCode)> followed = [];
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            foreach (var link in moving)
            {
                var from = link.Parcel.StatusId;
                var path = StatusCatalog.RouteDispatchPath(kind, from)
                           ?? throw BusinessException.Transition($"Le colis {link.ParcelId} ne peut pas partir depuis son statut.",
                               new { reason = "parcel-not-dispatchable", from, allowed = StatusCatalog.AllowedTransitions(from) });
                foreach (var statusId in path)
                {
                    db.ParcelEvents.Add(new ParcelEvent { ParcelId = link.ParcelId, StatusId = statusId, Comment = $"Circuit {route.Code}", AuthorId = caller.UserId, CityId = route.CityId, CreatedAt = now });
                }
                link.Parcel.StatusId = dispatched;
                link.Parcel.StatusAt = now;
                link.Parcel.CourierId = route.CourierId;
            }
            if (options.ParcelsPhase)
            {
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return moving.Count;
            }
            if (options.FinishPhase)
            {
                var claimed = await db.Routes.Where(r => r.Id == route.Id && r.Status == RouteStatus.DRAFT).ExecuteUpdateAsync(s => s.SetProperty(r => r.StatusAt, now), cancellationToken);
                if (claimed == 0) throw BusinessException.Conflict("Ce circuit est déjà parti.", new { reason = "route-dispatched" });
            }
            route.Status = RouteStatus.DISPATCHED;
            route.StatusAt = now;
            route.ExitDate ??= now;
            if (route.Kind == RouteKind.DELIVERY) followed = await FollowExchangeReturnsAsync(db, caller, route.Code, route.CourierId, route.CityId, links.Select(l => l.Parcel).ToList(), author, now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        await NotifyAsync(db, logger, route.CourierId, "route-assigned", new Dictionary<string, object> { ["count"] = links.Count }, new Dictionary<string, string> { ["routeCode"] = route.Code }, cancellationToken);
        foreach (var (returnCode, exchangeCode) in followed)
        {
            await NotifyAsync(db, logger, route.CourierId, "return-assigned", new Dictionary<string, object> { ["code"] = returnCode, ["exchange"] = exchangeCode },
                new Dictionary<string, string> { ["parcelCode"] = returnCode }, cancellationToken);
        }
        return moving.Count;
    }

    /// <summary>`followExchangeReturns` : le retour d'un échange suit le livreur qui porte l'échange, tant qu'il attend chez l'agence.</summary>
    internal static async Task<List<(string ReturnCode, string ExchangeCode)>> FollowExchangeReturnsAsync(IAppDbContext db, ICurrentUser caller, string routeCode, string courierId, string cityId, IReadOnlyList<Parcel> parcels, string author, DateTime now,
        CancellationToken cancellationToken)
    {
        var followed = new List<(string ReturnCode, string ExchangeCode)>();
        foreach (var exchange in parcels.Where(p => p.Kind == ParcelKind.EXCHANGE && p.ReferenceParcelId is not null))
        {
            var ret = await db.Parcels.Where(p => p.ReferenceParcelId == exchange.ReferenceParcelId && p.Kind == ParcelKind.RETURN && p.DeletedAt == null)
                .Select(p => new
                {
                    p.Id, p.Code, p.StatusId, p.CourierId, Courier = p.Courier == null ? null : p.Courier.FirstName + " " + p.Courier.LastName,
                    Held = p.HandoverItems.Any(i => i.Handover.Status != ReturnHandoverStatus.CANCELLED),
                }).FirstOrDefaultAsync(cancellationToken);
            if (ret is null || ret.StatusId != RouteStatusIds.ReturnPending || ret.CourierId == courierId || ret.Held) continue;
            var courier = await db.Couriers.AsNoTracking().Where(c => c.Id == courierId).Select(c => c.FirstName + " " + c.LastName).FirstOrDefaultAsync(cancellationToken);
            var after = courier ?? courierId;
            var reason = $"Suit l'échange {exchange.Code} (circuit {routeCode})";
            var tracked = await db.Parcels.FirstAsync(p => p.Id == ret.Id, cancellationToken);
            tracked.CourierId = courierId;
            db.ParcelEvents.Add(new ParcelEvent { ParcelId = ret.Id, StatusId = ret.StatusId, Comment = $"Affecté à {after} : {reason}", AuthorId = caller.UserId, AuthorName = author, CityId = cityId, CreatedAt = now });
            ParcelJournal.Record(db, caller, ret.Id, "courierId", ret.Courier, after, reason, author, now);
            followed.Add((ret.Code, exchange.Code));
        }
        return followed;
    }

    internal static async Task NotifyAsync(IAppDbContext db, ILogger logger, string courierId, string topic, Dictionary<string, object> parameters, Dictionary<string, string> data,
        CancellationToken cancellationToken)
    {
        try
        {
            await Notifier.NotifyCourierAsync(db, courierId, topic, parameters, data, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("Notification « {Topic} » du livreur {Courier} non envoyée : {Message}", topic, courierId, error.Message);
        }
    }
}

/// <summary>Ce que le livreur (ou le bureau) rend compte d'un colis du circuit (`routeConfirmSchema`, montants en dirhams).</summary>
public sealed record RouteOutcome(bool Confirmed, int? StatusId, string? ScheduledAt, string? Comment, double? CollectedAmount = null, double? Latitude = null, double? Longitude = null,
    double? ChequeAmount = null, string? ChequeNumber = null, string? ChequeBank = null, string? ChequeDate = null, bool? BlCollected = null, string? BlReference = null);

/// <summary>`prepareConfirmation` : statut, encaissement (espèces, chèque), commission, recette du livreur.</summary>
public static class RouteConfirmation
{
    /// <summary>Le circuit d'un compte rendu : le livreur n'atteint que les siens ; brouillon et clôturé refusent.</summary>
    public static async Task<Route> RouteAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken)
    {
        var route = await db.Routes.AsNoTracking().FirstOrDefaultAsync(r => r.Code == code && (caller.CourierId == null || r.CourierId == caller.CourierId), cancellationToken)
                    ?? throw BusinessException.NotFound($"Circuit introuvable : {code}");
        if (route.Status == RouteStatus.CLOSED) throw BusinessException.Conflict("Un circuit clôturé ne se modifie plus.", new { reason = "route-closed" });
        if (route.Status == RouteStatus.DRAFT) throw BusinessException.Conflict("Ce circuit n’est pas encore parti.", new { reason = "route-not-dispatched" });
        return route;
    }

    /// <summary>
    /// Écrit le compte rendu d'un lien suivi (`link.Parcel` chargé) ; l'appelant ouvre la transaction. Tous les
    /// montants restent en centimes jusqu'à l'écriture.
    /// </summary>
    public static async Task ApplyAsync(IAppDbContext db, ICurrentUser caller, Route route, RouteParcel link, RouteOutcome input, bool adminFieldRevision, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var parcel = link.Parcel;
        var target = input.StatusId;
        var kind = Money.ToWire(route.Kind);
        var courierIssue = caller.CourierId is not null && route.Kind == RouteKind.DELIVERY && target is not null;
        if (courierIssue && await HandoverRelease.HandedOverOfAsync(db, parcel.Id, cancellationToken) is { } handedOver)
        {
            throw BusinessException.Conflict($"Ce colis a été remis à l’agence sur le bon de retour {handedOver} : son statut ne change plus.", new { reason = "parcel-on-handover", handover = handedOver });
        }
        var releaseHandover = courierIssue && await HandoverRelease.HasPendingAsync(db, parcel.Id, cancellationToken);
        if (target is { } to && to != parcel.StatusId)
        {
            var courierRevision = caller.CourierId is not null && StatusCatalog.CanCourierReport(parcel.StatusId, to, kind);
            var adminRevision = adminFieldRevision && caller.IsSuperAdmin() && route.Kind == RouteKind.DELIVERY && StatusCatalog.CanAdminReviseField(parcel.StatusId, to);
            if (!courierRevision && !adminRevision && !StatusCatalog.CanTransition(parcel.StatusId, to))
            {
                throw BusinessException.Transition($"Transition {parcel.StatusId} → {to} interdite.", new { from = parcel.StatusId, to, allowed = StatusCatalog.AllowedTransitions(parcel.StatusId) });
            }
            if (caller.CourierId is not null && !StatusCatalog.CanCourierReport(parcel.StatusId, to, kind))
            {
                throw BusinessException.Transition($"Un livreur ne peut pas passer ce colis de {parcel.StatusId} à {to}.",
                    new { reason = "courier-transition-forbidden", from = parcel.StatusId, to, allowed = StatusCatalog.CourierOutcomesFor(parcel.StatusId, kind) });
            }
            if (parcel.BillingState != BillingState.TO_INVOICE)
            {
                throw BusinessException.Conflict("Ce colis est déjà facturé : son statut ne change plus (passez par un avoir).", new { reason = "parcel-invoiced" });
            }
        }

        long? collected = target == RouteStatusIds.Delivered
            ? CollectedAmountFor(caller, Billing.CodToCollect(Money.ToCents(parcel.CodAmount), Money.ToCents(parcel.DeliveryFee), parcel.FeeIncluded, parcel.AlreadyPaid), input.CollectedAmount)
            : null;
        var chequeAmount = collected is not null && input.ChequeAmount is { } cheque ? (long)Math.Round(cheque * 100, MidpointRounding.AwayFromZero) : 0;
        if (collected is { } total && chequeAmount > total)
        {
            throw BusinessException.Validation("Le chèque ne peut pas dépasser le montant encaissé.", new { reason = "cheque-over-collected", collected = total, cheque = chequeAmount });
        }
        var cashAmount = collected is null ? 0 : collected.Value - chequeAmount;
        var course = await CourseCommissionAsync(db, route, link, parcel, target, cancellationToken);
        var freeze = course is not null && (parcel.CourierCommission is null
                                            || (CourierPay.OutcomeOf(route.Kind.ToString(), target) == CourseOutcome.Delivery && parcel.CourierCommissionSource == CourierCommissionSource.FAILURE_SETTING))
            ? course
            : null;
        var cashDay = collected is null ? null
            : await db.Couriers.AsNoTracking().Where(c => c.Id == route.CourierId).Select(c => new { c.AgencyId, c.CityId, c.SettlementMode, c.PayModel }).FirstOrDefaultAsync(cancellationToken);
        var author = await ParcelStatusChange.AuthorNameAsync(db, caller.UserId, cancellationToken);
        var statusMoves = target is not null && target != parcel.StatusId;
        long? nextFailureFee = null;
        if (statusMoves)
        {
            var policy = await db.PricingPolicyAsync(cancellationToken, parcel.AgencyId);
            nextFailureFee = FailureFees.For(target!.Value, parcel.OriginCityId == parcel.DestinationCityId, policy);
        }

        if (releaseHandover) await HandoverRelease.ReleaseAsync(db, caller, parcel.Id, parcel.Code, target!.Value, author, now, cancellationToken);

        link.Confirmed = input.Confirmed;
        link.ConfirmedAt = input.Confirmed ? now : null;
        link.ConfirmedBy = input.Confirmed ? caller.UserId : null;
        link.FinalStatusId = target ?? link.FinalStatusId;
        if (collected is not null)
        {
            link.CodCollected = Money.FromCents(collected.Value);
            link.CodCash = Money.FromCents(cashAmount);
            link.CodCheque = Money.FromCents(chequeAmount);
        }
        if (input.Comment != string.Empty) link.Note = input.Comment ?? link.Note;
        if (course is { } c)
        {
            link.CourierCommission = Money.FromCents(c.Amount);
            link.CourierCommissionSource = Enum.Parse<CourierCommissionSource>(c.Source);
        }
        if (collected is not null && parcel.FinancialStatus == FinancialStatus.NON_ENCAISSE) parcel.FinancialStatus = FinancialStatus.ENCAISSE;
        await db.SaveChangesAsync(cancellationToken);

        string? remittanceId = null;
        var alreadyRemitted = collected is not null && await db.CashRemittanceParcels.AnyAsync(l => l.ParcelId == parcel.Id && l.ConfirmedParcelId == parcel.Id, cancellationToken);
        if (collected is not null && cashDay is not null && !alreadyRemitted)
        {
            remittanceId = await RemittanceLines.AttachDeliveredAsync(db, new DeliveredCash(route.CourierId, cashDay.AgencyId ?? parcel.AgencyId, cashDay.CityId, parcel.Id, cashAmount, chequeAmount,
                course?.Amount ?? 0, cashDay.SettlementMode, cashDay.PayModel), cancellationToken);
        }
        if (chequeAmount > 0)
        {
            await ChequeIntakes.AttachAsync(db, caller, new ChequeIntake(parcel.Id, route.CourierId, remittanceId, parcel.RecipientName, chequeAmount,
                string.IsNullOrWhiteSpace(input.ChequeNumber) ? "(non relevé)" : input.ChequeNumber.Trim(), string.IsNullOrWhiteSpace(input.ChequeBank) ? null : input.ChequeBank.Trim(),
                ParseDay(input.ChequeDate), author), cancellationToken);
        }
        if (freeze is { } f)
        {
            parcel.CourierCommission = Money.FromCents(f.Amount);
            parcel.CourierCommissionAt = now;
            parcel.CourierCommissionSource = Enum.Parse<CourierCommissionSource>(f.Source);
            parcel.CourierCommissionCourierId = route.CourierId;
        }
        if (!statusMoves)
        {
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var to2 = target!.Value;
        var failure = FailureFees.AfterTransition(parcel.IsFailed, Money.ToCentsOrNull(parcel.FailureFee), to2, StatusCatalog.IsFailure(to2), nextFailureFee);
        var from2 = parcel.StatusId;
        var scheduled = to2 == RouteStatusIds.Postponed && !string.IsNullOrEmpty(input.ScheduledAt) ? ParcelCreation.ParseDate(input.ScheduledAt) : null;
        await db.SaveChangesAsync(cancellationToken);
        var moved = await db.Parcels.Where(p => p.Id == parcel.Id && p.StatusId == from2).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.StatusId, to2)
            .SetProperty(p => p.StatusAt, now)
            .SetProperty(p => p.IsFailed, failure.IsFailed)
            .SetProperty(p => p.FailureFee, p => failure.Keep ? p.FailureFee : failure.FailureFee == null ? null : Money.FromCents(failure.FailureFee.Value))
            .SetProperty(p => p.ScheduledAt, p => scheduled ?? p.ScheduledAt), cancellationToken);
        if (moved == 0) throw BusinessException.Conflict("Le statut de ce colis vient de changer : rechargez avant de recommencer.", new { reason = "parcel-status-changed", from = from2 });
        db.ParcelEvents.Add(new ParcelEvent
        {
            ParcelId = parcel.Id, StatusId = to2, Comment = string.IsNullOrEmpty(input.Comment) ? $"Circuit {route.Code}" : input.Comment, AuthorId = caller.UserId, AuthorName = author, CityId = route.CityId,
            ScheduledDate = scheduled, Latitude = input.Latitude is { } lat ? (decimal)lat : null, Longitude = input.Longitude is { } lng ? (decimal)lng : null, CreatedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>`collectedAmountFor` : le livreur encaisse le dû, rien d'autre ; le bureau peut saisir un autre montant.</summary>
    public static long CollectedAmountFor(ICurrentUser caller, long due, double? posted)
    {
        if (posted is not { } value) return due;
        var collected = (long)Math.Round(value * 100, MidpointRounding.AwayFromZero);
        if (caller.CourierId is not null && collected != due)
        {
            throw BusinessException.Validation("Le montant encaissé doit être le montant à encaisser du colis. Seul le chèque se saisit.", new { reason = "collected-not-due", due, collected });
        }
        return collected;
    }

    /// <summary>`courseCommission` : la commission de la course selon son issue (négociée, échec, grille).</summary>
    private static async Task<(long Amount, string Source)?> CourseCommissionAsync(IAppDbContext db, Route route, RouteParcel link, Parcel parcel, int? target, CancellationToken cancellationToken)
    {
        if (CourierPay.OutcomeOf(route.Kind.ToString(), target) is not { } outcome) return null;
        var grid = await db.CourierCities.AsNoTracking().Where(c => c.CourierId == route.CourierId).Select(c => new { c.CityId, c.PickupFee, c.DeliveryFee }).ToListAsync(cancellationToken);
        var failed = await db.GetSettingAsync(SettingKeys.CourierFailedCommission, 0L, cancellationToken);
        return CourierPay.CommissionFor(outcome, outcome == CourseOutcome.Pickup ? parcel.OriginCityId : parcel.DestinationCityId, Money.ToCentsOrNull(link.CourierFee),
            grid.Select(g => new CourierGridRate(g.CityId, Money.ToCents(g.PickupFee), Money.ToCents(g.DeliveryFee))).ToList(), failed);
    }

    private static DateOnly? ParseDay(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null
        : DateOnly.TryParse(text.Trim().Length >= 10 ? text.Trim()[..10] : text.Trim(), CultureInfo.InvariantCulture, out var day) ? day : null;
}

/// <summary>`closeRoute`, `autoCloseDeliveryRoute`, `autoCloseReturnRoute`.</summary>
public static class RouteClosing
{
    public sealed record Automatic(int Delivered, int Returned, int Total, string? Summary = null);

    public static async Task CloseAsync(IAppDbContext db, ICurrentUser caller, ILogger logger, Route route, bool removeUnpicked, Automatic? automatic, CancellationToken cancellationToken)
    {
        if (route.Status == RouteStatus.CLOSED) throw BusinessException.Conflict("Ce circuit est déjà clôturé.");
        // La fiche ne propose la clôture qu'aux circuits non brouillons de ramassage et de retour (`closable`) : l'API refuse
        // ce que l'écran refuse. Un circuit de LIVRAISON se clôt automatiquement (D-155), quand chaque colis est livré ou
        // rendu à l'agence — jamais à la main, sans quoi ses colis à retenter se retrouvent hors de toute tournée.
        if (automatic is null && route.Kind == RouteKind.DELIVERY)
        {
            throw BusinessException.Conflict("Un circuit de livraison se clôture automatiquement, quand chaque colis est livré ou rendu à l’agence.",
                new { reason = "delivery-route-auto-close" });
        }
        if (automatic is null && route.Status == RouteStatus.DRAFT)
        {
            throw BusinessException.Conflict("Un circuit brouillon ne se clôture pas : supprimez-le ou faites-le partir.", new { reason = "route-draft" });
        }
        var links = await db.RouteParcels.AsNoTracking().Where(l => l.RouteId == route.Id).Select(l => new ClosurePlans.Link(l.ParcelId, l.Parcel.Code, l.Confirmed, l.Parcel.StatusId)).ToListAsync(cancellationToken);
        var total = links.Count;
        if (total == 0 && automatic is null) throw BusinessException.Validation("Un circuit vide ne se clôture pas.", new { total });
        var plan = ClosurePlans.Of(route.Kind, links);
        if (plan.Blocking.Count > 0 && automatic is null)
        {
            var codes = plan.Blocking.Select(l => l.Code).ToList();
            var shown = string.Join(", ", codes.Take(10)) + (codes.Count > 10 ? "…" : string.Empty);
            throw BusinessException.Validation(route.Kind == RouteKind.PICKUP
                    ? $"{codes.Count} colis sur {total} ont un statut qui empêche la clôture : {shown}."
                    : $"{codes.Count} colis sur {total} sans résultat : {shown}. Posez leur issue avant de clôturer.",
                new { reason = "route-parcels-on-the-road", count = codes.Count, total, codes = codes.Take(50).ToList() });
        }
        if (route.Kind == RouteKind.PICKUP)
        {
            if (plan.Picked == 0)
            {
                throw BusinessException.Validation("Aucun colis ramassé sur ce circuit : rien à clôturer. Retirez ses bons ou ramassez d’abord.", new { reason = "route-nothing-picked", total });
            }
            if (plan.Unpicked.Count > 0 && !removeUnpicked)
            {
                var codes = plan.Unpicked.Select(l => l.Code).ToList();
                throw BusinessException.Validation(
                    $"{plan.Picked} colis ramassés sur {total}. Les {codes.Count} autres restent en attente de ramassage, hors du bon et du circuit : confirmez pour clôturer.",
                    new { reason = "route-unpicked-parcels", picked = plan.Picked, total, count = codes.Count, codes = codes.Take(50).ToList() });
            }
        }

        var pending = route.Kind == RouteKind.PICKUP
            ? await db.DeliveryNotes.Where(n => n.Status != DeliveryNoteStatus.VALIDATED && n.Routes.Any(r => r.RouteId == route.Id)).Include(n => n.Parcels).ThenInclude(l => l.Parcel).ToListAsync(cancellationToken)
            : [];
        var policies = new Dictionary<string, PricingPolicy>();
        foreach (var note in pending.Where(n => !policies.ContainsKey(n.AgencyId))) policies[note.AgencyId] = await NoteRules.PolicyAsync(db, note.AgencyId, cancellationToken);
        var now = DateTime.UtcNow;
        var unpickedIds = plan.Unpicked.Select(l => l.Id).ToHashSet();
        var byNote = pending.Select(n => (Note: n, Parcels: n.Parcels.Select(l => l.Parcel).Where(p => unpickedIds.Contains(p.Id)).ToList())).Where(g => g.Parcels.Count > 0).ToList();
        var onANote = byNote.SelectMany(g => g.Parcels.Select(p => p.Id)).ToHashSet();
        var loose = plan.Unpicked.Where(l => !onANote.Contains(l.Id)).ToList();
        var routesOf = new Dictionary<string, List<string>>();
        foreach (var (note, _) in byNote) routesOf[note.Id] = await NoteRules.OpenRoutesOfNoteAsync(db, note.Id, cancellationToken);
        var author = await ParcelStatusChange.AuthorNameAsync(db, caller.UserId, cancellationToken);

        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var claimed = await db.Routes.Where(r => r.Id == route.Id && r.Status != RouteStatus.CLOSED)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, RouteStatus.CLOSED).SetProperty(r => r.StatusAt, now), cancellationToken);
            if (claimed == 0) throw BusinessException.Conflict("Ce circuit vient d’être clôturé.", new { reason = "route-already-closed" });
            if (automatic is not null)
            {
                db.AuditLogs.Add(new AuditLog
                {
                    Entity = "Route", EntityId = route.Id, Action = AuditAction.UPDATE, Before = JsonSerializer.Serialize(new { status = route.Status.ToString() }),
                    After = JsonSerializer.Serialize(new
                    {
                        status = "CLOSED", automatic = true,
                        summary = automatic.Summary ?? $"Clôturé automatiquement : {automatic.Total} colis, {automatic.Delivered} livrés, {automatic.Returned} retournés",
                    }),
                    Diff = ["status"], UserId = caller.UserId, At = now,
                });
            }
            foreach (var (note, parcels) in byNote)
            {
                await NoteRules.RemoveFromNoteAsync(db, caller, note, parcels, routesOf.GetValueOrDefault(note.Id) is { Count: > 0 } ids ? ids : [route.Id], author, route.CityId, now, cancellationToken);
            }
            foreach (var link in loose)
            {
                await db.RouteParcels.Where(l => l.RouteId == route.Id && l.ParcelId == link.Id).ExecuteDeleteAsync(cancellationToken);
                if (link.StatusId != NoteRules.PickupInProgress) continue;
                var parcel = await db.Parcels.FirstAsync(p => p.Id == link.Id, cancellationToken);
                parcel.StatusId = NoteRules.AwaitingPickup;
                parcel.StatusAt = now;
                db.ParcelEvents.Add(new ParcelEvent
                {
                    ParcelId = link.Id, StatusId = NoteRules.AwaitingPickup, Comment = $"Non ramassé — retiré du circuit {route.Code} à sa clôture", AuthorId = caller.UserId, AuthorName = author,
                    CityId = route.CityId, CreatedAt = now,
                });
            }
            await db.SaveChangesAsync(cancellationToken);
            foreach (var note in pending)
            {
                await NoteRules.ApplyValidationAsync(db, caller, note, note.Parcels.Select(l => l.Parcel).Where(p => !unpickedIds.Contains(p.Id)).ToList(), policies[note.AgencyId], now, cancellationToken,
                    comment: $"Clôture du circuit {route.Code}");
            }
            var onAPendingNote = pending.SelectMany(n => n.Parcels.Select(l => l.ParcelId)).ToHashSet();
            foreach (var link in links)
            {
                if (onAPendingNote.Contains(link.Id) || unpickedIds.Contains(link.Id)) continue;
                if (link.StatusId != NoteRules.PickedUpByCourier && !(link.Confirmed && link.StatusId == NoteRules.PickupInProgress)) continue;
                var parcel = await db.Parcels.FirstAsync(p => p.Id == link.Id, cancellationToken);
                parcel.StatusId = NoteRules.PickedUp;
                parcel.StatusAt = now;
                db.ParcelEvents.Add(new ParcelEvent { ParcelId = link.Id, StatusId = NoteRules.PickedUp, Comment = $"Clôture du circuit {route.Code}", AuthorId = caller.UserId, AuthorName = author, CityId = route.CityId, CreatedAt = now });
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        foreach (var (note, parcels) in byNote) await NoteRules.AnnounceRemovalAsync(db, logger, note, parcels.Count, cancellationToken);
    }

    /// <summary>Le circuit de livraison se clôt seul quand chaque colis est livré ou reçu par l'agence (D-155).</summary>
    public static async Task<Automatic?> AutoCloseDeliveryAsync(IAppDbContext db, ICurrentUser caller, ILogger logger, string routeId, CancellationToken cancellationToken)
    {
        var route = await db.Routes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == routeId && r.Kind == RouteKind.DELIVERY && (r.Status == RouteStatus.DISPATCHED || r.Status == RouteStatus.DELIVERING), cancellationToken);
        if (route is null) return null;
        var parcels = await db.RouteParcels.AsNoTracking().Where(l => l.RouteId == route.Id).Select(l => new { l.Parcel.Id, l.Parcel.StatusId }).ToListAsync(cancellationToken);
        var completion = await RouteRows.DeliveryCompletionAsync(db, parcels.Select(p => (p.Id, p.StatusId)).ToList(), cancellationToken);
        if (completion.ToReturn > 0) return null;
        var summary = new Automatic(completion.Delivered, completion.Returned, parcels.Count);
        try
        {
            await CloseAsync(db, caller, logger, route, false, summary, cancellationToken);
        }
        catch (BusinessException error) when (error.Code == ErrorCode.CONFLICT)
        {
            db.ChangeTracker.Clear();
            return null;
        }
        return summary;
    }

    public static async Task AutoCloseReturnAsync(IAppDbContext db, ICurrentUser caller, ILogger logger, string routeId, CancellationToken cancellationToken)
    {
        var route = await db.Routes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == routeId && r.Kind == RouteKind.RETURN && (r.Status == RouteStatus.DISPATCHED || r.Status == RouteStatus.DELIVERING), cancellationToken);
        if (route is null) return;
        var links = await db.RouteParcels.AsNoTracking().Where(l => l.RouteId == route.Id).Select(l => new ClosurePlans.Link(l.ParcelId, l.Parcel.Code, l.Confirmed, l.Parcel.StatusId)).ToListAsync(cancellationToken);
        if (links.Count == 0 || ClosurePlans.Of(RouteKind.RETURN, links).Blocking.Count > 0) return;
        var handedBack = links.Count(l => l.StatusId == RouteStatusIds.ReturnedToSender);
        try
        {
            await CloseAsync(db, caller, logger, route, false, new Automatic(handedBack, links.Count - handedBack, links.Count,
                $"Clôturé automatiquement : {links.Count} colis, {handedBack} rendus au vendeur, {links.Count - handedBack} non remis"), cancellationToken);
        }
        catch (BusinessException error) when (error.Code == ErrorCode.CONFLICT)
        {
            db.ChangeTracker.Clear();
        }
    }

    public static async Task AfterOutcomeAsync(IAppDbContext db, ICurrentUser caller, ILogger logger, Route route, CancellationToken cancellationToken)
    {
        if (route.Kind == RouteKind.DELIVERY) await AutoCloseDeliveryAsync(db, caller, logger, route.Id, cancellationToken);
        if (route.Kind == RouteKind.RETURN) await AutoCloseReturnAsync(db, caller, logger, route.Id, cancellationToken);
    }
}
