using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Invoices;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using Delivery.Domain.Parcels;
using Delivery.Domain.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Routes;

public sealed record CorrectionCourierDto(string Id, string Name, bool CoversCity);

public sealed record CorrectionRouteDto(string Code, string Status, string CityName);

public sealed record StatusCorrectionPreviewDto(string Code, int From, int To, string Family, string? CourierId, string? RouteCode, bool NeedsCourier,
    IReadOnlyList<CorrectionCourierDto> Couriers, IReadOnlyList<CorrectionRouteDto> Routes, IReadOnlyList<Dictionary<string, object?>> Steps, string Fingerprint);

public sealed record StatusCorrectionResultDto(string Code, int From, int To, IReadOnlyList<Dictionary<string, object?>> Steps);

/// <summary>Une demande de correction (`statusCorrectionSchema`, déjà validée).</summary>
public sealed record CorrectionInput(int StatusId, string Reason, string? CourierId, string? RouteCode, string? ScheduledDate, string? Fingerprint);

/// <summary>`status-correction.ts` : les familles de statuts qu'une correction sait poser.</summary>
public static class CorrectionFamilies
{
    private static readonly (string Family, int[] Ids)[] Families =
    [
        ("before-pickup", [1, 2, 38, 19]),
        ("at-agency", [3, 16, 31, 7, 18, 15]),
        ("delivery", [13, 6, 8, 9, 10, 11, 14, 25, 26, 27, 28, 29, 32, 33, 34, 35, 37, 30, 36]),
        ("return", [20, 22, 23]),
        ("delivered", [4]),
        ("route-born", [12, 17, 24, 21, 5]),
    ];

    public static string? FamilyOf(int statusId) => Families.FirstOrDefault(f => f.Ids.Contains(statusId)).Family;

    public static bool IsTarget(int statusId) => FamilyOf(statusId) is { } family && family != "route-born";
}

/// <summary>
/// `StatusCorrectionService` : l'administrateur corrige librement un statut ; le plan aligne bons, circuits, retours,
/// frais d'échec et factures brouillons. D'un colis livré, l'annulation de la livraison (argent, commission, ligne RF-)
/// et la correction s'écrivent dans la MÊME transaction, avec leur journal d'audit.
/// </summary>
public static class StatusCorrection
{
    private const int PickedUpByCourier = 18;
    private static readonly int[] ReturnedIn = [22, 23];
    private static readonly ReturnHandoverStatus[] HandoverActive = [ReturnHandoverStatus.DRAFT, ReturnHandoverStatus.RETURN_REQUESTED, ReturnHandoverStatus.PICKED_UP, ReturnHandoverStatus.AWAITING_RECEIPT];
    private static readonly ReturnHandoverStatus[] HandoverReceived = [ReturnHandoverStatus.CONFIRMED, ReturnHandoverStatus.CLOSED];

    private sealed record ParcelRow(string Id, string Code, int StatusId, DateTime StatusAt, BillingState BillingState, FinancialStatus FinancialStatus, bool ReimbursedByCourier, bool IsFailed,
        decimal? FailureFee, string? CourierId, string AgencyId, string OriginCityId, string DestinationCityId, string? CurrentCityId, decimal CodAmount, decimal DeliveryFee, bool FeeIncluded,
        bool AlreadyPaid);

    private sealed record LineRow(string RouteId, int? FinalStatusId, bool Confirmed, DateTime? CorrectedAt, string Code, RouteStatus Status, RouteKind Kind, string CourierId, string CityId, DateTime CreatedAt);

    private sealed record RouteChoice(string Id, string Code, RouteStatus Status, string CityId);

    private sealed record Placement(string CourierId, string CourierName, RouteChoice? Route, bool Reuse);

    private sealed record Ref(string Id, string Code);

    private sealed record Leave(string Id, string Code, RouteStatus Status, RouteKind Kind);

    private sealed record Plan(ParcelRow Parcel, int From, int To, string Family, int StatusInTx, bool IsFailed, long? FailureFee, bool KeepFee, IReadOnlyList<Ref> LeaveNotes,
        IReadOnlyList<Leave> LeaveRoutes, IReadOnlyList<Ref> LeaveHandovers, IReadOnlyList<Ref> ReleaseHandovers, IReadOnlyList<Ref> MarkRoutes, IReadOnlyList<Ref> MarkNotes, Placement? Placement,
        bool ConfirmOnRoute, IReadOnlyList<string> InvoiceDrafts, bool NeedsCourier, IReadOnlyList<CorrectionCourierDto> Couriers, IReadOnlyList<CorrectionRouteDto> Routes,
        List<Dictionary<string, object?>> Steps, string Fingerprint);

    public static void AssertAdmin(ICurrentUser caller)
    {
        if (!caller.IsSuperAdmin()) throw BusinessException.Forbidden("Seul un administrateur corrige librement un statut.", new { reason = "correction-admin-only" });
    }

    public static async Task<StatusCorrectionPreviewDto> PreviewAsync(IAppDbContext db, string code, CorrectionInput input, CancellationToken cancellationToken, bool keep = false)
    {
        var plan = await PlanAsync(db, code, input, false, keep, cancellationToken);
        return new StatusCorrectionPreviewDto(plan.Parcel.Code, plan.From, plan.To, plan.Family, plan.Placement?.CourierId, plan.Placement?.Route?.Code, plan.NeedsCourier, plan.Couriers,
            plan.Routes, plan.Steps, plan.Fingerprint);
    }

    /// <param name="keep">`keepAttachments` : le miroir d'un prestataire suit le statut sans détacher bons, circuits ni retours.</param>
    public static async Task<StatusCorrectionResultDto> ApplyAsync(IAppDbContext db, ICurrentUser caller, IClock clock, ILogger logger, string code, CorrectionInput input,
        CancellationToken cancellationToken, bool keep = false)
    {
        var reason = input.Reason.Trim();
        if (reason.Length == 0) throw BusinessException.Validation("Une correction de statut exige un motif.", new { reason = "reason-required", field = "reason" });
        var first = await PlanAsync(db, code, input, false, keep, cancellationToken);
        if (!string.IsNullOrEmpty(input.Fingerprint) && input.Fingerprint != first.Fingerprint)
        {
            throw BusinessException.Conflict("Le colis a changé depuis l’aperçu : relisez ce que la correction va faire avant de valider.", new { reason = "correction-plan-changed" });
        }
        if (first.NeedsCourier) throw BusinessException.Validation("Choisissez le livreur qui reprend ce colis.", new { reason = "courier-required", field = "courierId" });

        var author = await ParcelStatusChange.AuthorNameAsync(db, caller.UserId, cancellationToken);
        var now = clock.UtcNow;
        var prefix = new List<Dictionary<string, object?>>();
        DeliveryCancel.Plan? cancel = null;
        Plan plan;
        RouteChoice? route;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            if (first.From == RouteStatusIds.Delivered)
            {
                cancel = await DeliveryCancel.PlanAsync(db, code, cancellationToken);
                var remittance = await DeliveryCancel.WriteAsync(db, caller, cancel, $"Correction du statut : {reason}", author, now, cancellationToken);
                prefix.Add(Step("cancel-delivery", ("routeCode", cancel.Route?.Code), ("routeReopened", cancel.Reopen), ("remittanceCode", remittance)));
                plan = await PlanAsync(db, code, input, true, keep, cancellationToken);
            }
            else
            {
                plan = first;
            }
            route = await WriteAsync(db, caller, plan, input, reason, author, now, [.. prefix, .. plan.Steps], cancellationToken);
            if (plan.ConfirmOnRoute && route is not null)
            {
                // L'issue (et l'encaissement d'un « Livré ») sur le circuit parti, dans la même transaction.
                var routeRow = await db.Routes.AsNoTracking().FirstAsync(r => r.Id == route.Id, cancellationToken);
                var link = await db.RouteParcels.Include(l => l.Parcel).FirstAsync(l => l.RouteId == route.Id && l.ParcelId == plan.Parcel.Id, cancellationToken);
                await RouteConfirmation.ApplyAsync(db, caller, routeRow, link, new RouteOutcome(true, plan.To, plan.To == RouteStatusIds.Postponed ? input.ScheduledDate ?? string.Empty : string.Empty,
                    $"Correction du statut : {reason}{PlacedOn(plan, route)}"), false, cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        db.ChangeTracker.Clear();
        if (cancel is not null) await DeliveryCancel.SettleAsync(db, logger, cancel, cancellationToken);
        await SettleAsync(db, caller, logger, plan, route, cancellationToken);
        if (plan.ConfirmOnRoute && route is not null)
        {
            var routeRow = await db.Routes.AsNoTracking().FirstAsync(r => r.Id == route.Id, cancellationToken);
            await RouteClosing.AfterOutcomeAsync(db, caller, logger, routeRow, cancellationToken);
            var clientId = await db.Parcels.AsNoTracking().Where(p => p.Id == plan.Parcel.Id).Select(p => p.ClientId).FirstAsync(cancellationToken);
            await ParcelStatusChange.NotifyMerchantOfStatusAsync(db, logger, clientId, plan.Parcel.Code, plan.To, plan.To == RouteStatusIds.Postponed ? input.ScheduledDate : null, cancellationToken);
        }
        return new StatusCorrectionResultDto(plan.Parcel.Code, first.From, plan.To, [.. prefix, .. plan.Steps]);
    }

    private static string PlacedOn(Plan plan, RouteChoice? route) => route is not null && plan.Placement is not null ? $" ({plan.Placement.CourierName}, {route.Code})" : string.Empty;

    private static Dictionary<string, object?> Step(string kind, params (string Key, object? Value)[] fields)
    {
        var step = new Dictionary<string, object?> { ["kind"] = kind };
        foreach (var (key, value) in fields) step[key] = value;
        return step;
    }

    private static async Task<Plan> PlanAsync(IAppDbContext db, string code, CorrectionInput input, bool afterCancel, bool keep, CancellationToken cancellationToken)
    {
        var loaded = await db.Parcels.AsNoTracking().Where(p => p.Code == code && p.DeletedAt == null)
                         .Select(p => new ParcelRow(p.Id, p.Code, p.StatusId, p.StatusAt, p.BillingState, p.FinancialStatus, p.ReimbursedByCourier, p.IsFailed, p.FailureFee, p.CourierId, p.AgencyId,
                             p.OriginCityId, p.DestinationCityId, p.CurrentCityId, p.CodAmount, p.DeliveryFee, p.FeeIncluded, p.AlreadyPaid))
                         .FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound($"Colis introuvable : {code}");
        var parcel = loaded;
        var to = input.StatusId;
        if (StatusCatalog.Find(to) is null) throw BusinessException.Validation($"Statut inconnu : {to}", new { reason = "unknown-status" });
        var family = CorrectionFamilies.FamilyOf(to);
        if (family is null || (!CorrectionFamilies.IsTarget(to) && !keep))
        {
            var legacy = to is 21 or 5;
            throw BusinessException.Transition(legacy
                    ? "Ce statut est historique : aucun geste ne le pose plus. Choisissez un statut actuel."
                    : "Ce statut naît du départ d’un circuit : choisissez le statut d’avant, puis composez le circuit.",
                new { reason = legacy ? "target-legacy" : "target-needs-route", to });
        }
        await AssertMoneyFreeAsync(db, parcel, cancellationToken);
        string? reopenedRouteId = null;
        var steps = new List<Dictionary<string, object?>>();
        if (parcel.StatusId == RouteStatusIds.Delivered && !afterCancel)
        {
            if (to == RouteStatusIds.Delivered) throw BusinessException.Conflict("Ce colis est déjà livré.", new { reason = "same-status" });
            var preview = await DeliveryCancel.PlanAsync(db, code, cancellationToken);
            steps.Add(Step("cancel-delivery", ("routeCode", preview.Route?.Code), ("routeReopened", preview.Reopen), ("remittanceCode", preview.RemittanceCode)));
            reopenedRouteId = preview.Reopen ? preview.Route?.RouteId : null;
            parcel = parcel with { StatusId = preview.Target, FinancialStatus = FinancialStatus.NON_ENCAISSE };
        }
        var from = afterCancel ? RouteStatusIds.Delivered : loaded.StatusId;
        var current = parcel.StatusId;

        var notes = await db.DeliveryNoteParcels.AsNoTracking().Where(l => l.ParcelId == parcel.Id)
            .Select(l => new { l.CorrectedAt, l.DeliveryNote.Id, l.DeliveryNote.Code, l.DeliveryNote.Status }).ToListAsync(cancellationToken);
        var lines = (await db.RouteParcels.AsNoTracking().Where(l => l.ParcelId == parcel.Id)
                .Select(l => new LineRow(l.RouteId, l.FinalStatusId, l.Confirmed, l.CorrectedAt, l.Route.Code, l.Route.Status, l.Route.Kind, l.Route.CourierId, l.Route.CityId, l.Route.CreatedAt))
                .ToListAsync(cancellationToken))
            .Select(l => l.RouteId == reopenedRouteId ? l with { FinalStatusId = null, Confirmed = false, Status = RouteStatus.DISPATCHED } : l).ToList();
        var items = await db.ReturnHandoverItems.AsNoTracking().Where(i => i.ParcelId == parcel.Id && i.Handover.Status != ReturnHandoverStatus.CANCELLED)
            .Select(i => new { i.Id, i.CorrectedAt, i.Handover.Code, i.Handover.Status }).ToListAsync(cancellationToken);
        var invoiceCodes = await db.InvoiceLines.AsNoTracking().Where(l => l.ParcelId == parcel.Id && l.InvoicedParcelId == null && l.Invoice.Status == InvoiceStatus.DRAFT)
            .Select(l => l.Invoice.Code).ToListAsync(cancellationToken);

        var openLines = lines.Where(l => l.Status != RouteStatus.CLOSED).ToList();
        var activeNotes = notes.Where(n => n.Status != DeliveryNoteStatus.VALIDATED).ToList();
        var activeItems = items.Where(i => HandoverActive.Contains(i.Status)).ToList();
        var receivedItems = items.Where(i => HandoverReceived.Contains(i.Status) && i.CorrectedAt is null).ToList();
        if (keep)
        {
            var handedOver = items.FirstOrDefault(i => i.CorrectedAt is null && (i.Status == ReturnHandoverStatus.AWAITING_RECEIPT || HandoverReceived.Contains(i.Status)));
            if (handedOver is not null)
            {
                throw BusinessException.Conflict($"Ce colis est sur le bon de retour {handedOver.Code}, remis à l’agence : son statut ne suit plus le prestataire.",
                    new { reason = "parcel-on-handover", handoverCode = handedOver.Code });
            }
        }
        if (!keep && to == PickedUpByCourier && lines.Any(l => l.Kind == RouteKind.PICKUP && l.Status == RouteStatus.CLOSED))
        {
            throw BusinessException.Transition(
                "« Ramassé par le livreur » ne se pose que sur un circuit de ramassage en cours : ce colis a déjà le sien, clôturé. Choisissez « Colis ramassé » ou « Attente de ramassage ».",
                new { reason = "target-needs-route", to });
        }
        var onDelivery = family is "delivery" or "delivered";
        var leaveNotes = !keep && family == "before-pickup" ? activeNotes.Select(n => new Ref(n.Id, n.Code)).ToList() : [];
        var leaveHandovers = keep || to == RouteStatusIds.ReturnPending ? [] : activeItems.Select(i => new Ref(i.Id, i.Code)).ToList();
        var releaseHandovers = keep || ReturnedIn.Contains(to) ? [] : receivedItems.Select(i => new Ref(i.Id, i.Code)).ToList();
        Placement? placement = null;
        var needsCourier = false;
        IReadOnlyList<CorrectionCourierDto> couriers = [];
        IReadOnlyList<CorrectionRouteDto> routeOptions = [];
        if (onDelivery) (placement, needsCourier, couriers, routeOptions) = await PlaceAsync(db, parcel, lines, input, cancellationToken);
        var leaveRoutes = (keep || to == RouteStatusIds.ReturnPending ? [] : openLines)
            .Where(l => !(placement is { Reuse: true } && l.RouteId == placement.Route?.Id))
            .Select(l => new Leave(l.RouteId, l.Code, l.Status, l.Kind)).ToList();
        var markRoutes = lines.Where(l => l.Status == RouteStatus.CLOSED && l.CorrectedAt is null).Select(l => new Ref(l.RouteId, l.Code)).ToList();
        var markNotes = notes.Where(n => n.Status == DeliveryNoteStatus.VALIDATED && n.CorrectedAt is null).Select(n => new Ref(n.Id, n.Code)).ToList();
        var routeRunning = placement?.Route is { Status: RouteStatus.DISPATCHED or RouteStatus.DELIVERING };
        if (family == "delivered" && placement is not null && !routeRunning)
        {
            throw BusinessException.Conflict(
                "« Livré » se pose sur un circuit de livraison PARTI : ce livreur n’en a aucun d’ouvert. Choisissez un autre circuit, ou corrigez vers « En cours de livraison » puis faites partir le circuit.",
                new { reason = "deliver-needs-dispatched-route" });
        }
        var confirmOnRoute = onDelivery && to != RouteStatusIds.OutForDelivery && routeRunning;
        var statusInTx = confirmOnRoute ? RouteStatusIds.OutForDelivery : to;
        var policy = await db.PricingPolicyAsync(cancellationToken, parcel.AgencyId);
        var sameCity = parcel.OriginCityId == parcel.DestinationCityId;
        var beforeFee = Money.ToCentsOrNull(parcel.FailureFee);
        var inTx = FailureFees.AfterTransition(parcel.IsFailed, beforeFee, statusInTx, StatusCatalog.IsFailure(statusInTx), FailureFees.For(statusInTx, sameCity, policy));
        var feeAfterTx = inTx.Keep ? beforeFee : inTx.FailureFee;
        var final = confirmOnRoute ? FailureFees.AfterTransition(inTx.IsFailed, feeAfterTx, to, StatusCatalog.IsFailure(to), FailureFees.For(to, sameCity, policy)) : inTx;
        var finalFee = final.Keep ? feeAfterTx : final.FailureFee;
        var invoiceDrafts = StatusCatalog.IsBillableParcel(to, final.IsFailed) ? [] : invoiceCodes.Distinct().ToList();

        foreach (var note in leaveNotes) steps.Add(Step("leave-note", ("code", note.Code)));
        foreach (var item in leaveHandovers) steps.Add(Step("leave-handover", ("code", item.Code)));
        foreach (var item in releaseHandovers) steps.Add(Step("release-handover", ("code", item.Code)));
        foreach (var leave in leaveRoutes) steps.Add(Step("leave-route", ("code", leave.Code)));
        foreach (var mark in markRoutes) steps.Add(Step("mark-route", ("code", mark.Code)));
        foreach (var mark in markNotes) steps.Add(Step("mark-note", ("code", mark.Code)));
        if (placement is not null)
        {
            if (placement.Route is null) steps.Add(Step("open-route", ("courierName", placement.CourierName)));
            else if (placement.Reuse) steps.Add(Step("reuse-line", ("routeCode", placement.Route.Code), ("courierName", placement.CourierName)));
            else steps.Add(Step("place-on-route", ("routeCode", placement.Route.Code), ("courierName", placement.CourierName)));
        }
        steps.Add(Step("set-status", ("from", current), ("to", statusInTx)));
        if (confirmOnRoute && placement?.Route is { } onRoute)
        {
            steps.Add(to == RouteStatusIds.Delivered
                ? Step("deliver", ("routeCode", onRoute.Code), ("amount", Billing.CodToCollect(Money.ToCents(parcel.CodAmount), Money.ToCents(parcel.DeliveryFee), parcel.FeeIncluded, parcel.AlreadyPaid)))
                : Step("confirm-outcome", ("routeCode", onRoute.Code), ("statusId", to)));
        }
        if (final.IsFailed != parcel.IsFailed) steps.Add(Step("failure-flag", ("before", parcel.IsFailed), ("after", final.IsFailed)));
        if (finalFee != beforeFee) steps.Add(Step("failure-fee", ("before", beforeFee), ("after", finalFee)));
        foreach (var invoice in invoiceDrafts) steps.Add(Step("leave-invoice-draft", ("code", invoice)));
        if (family == "return" && parcel.CourierId is not null)
        {
            var courierName = await db.Couriers.AsNoTracking().Where(c => c.Id == parcel.CourierId).Select(c => c.FirstName + " " + c.LastName).FirstOrDefaultAsync(cancellationToken);
            if (courierName is not null) steps.Add(Step("courier-kept", ("courierName", courierName.Trim())));
        }
        if (from == to && !needsCourier)
        {
            var reused = placement is { Reuse: true } ? lines.FirstOrDefault(l => l.RouteId == placement.Route?.Id) : null;
            var lineMoves = reused is not null && (reused.FinalStatusId != (confirmOnRoute ? to : null) || reused.Confirmed != confirmOnRoute);
            string[] quiet = ["set-status", "reuse-line", "confirm-outcome", "mark-route", "mark-note", "courier-kept"];
            if (!lineMoves && steps.All(s => quiet.Contains((string)s["kind"]!)))
            {
                throw BusinessException.Conflict("Ce colis porte déjà ce statut, et tout est aligné : rien à corriger.", new { reason = "nothing-to-correct" });
            }
        }
        var fingerprint = Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            from, to, statusAt = loaded.StatusAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"), courierId = placement?.CourierId, routeCode = placement?.Route?.Code, steps,
        }, JsonSerializerOptions.Web))));
        return new Plan(parcel, from, to, family, statusInTx, inTx.IsFailed, inTx.FailureFee, inTx.Keep, leaveNotes, leaveRoutes, leaveHandovers, releaseHandovers, markRoutes, markNotes, placement,
            confirmOnRoute, invoiceDrafts, needsCourier, couriers, routeOptions, steps, fingerprint);
    }

    private static async Task<(Placement?, bool, IReadOnlyList<CorrectionCourierDto>, IReadOnlyList<CorrectionRouteDto>)> PlaceAsync(IAppDbContext db, ParcelRow parcel, IReadOnlyList<LineRow> lines,
        CorrectionInput input, CancellationToken cancellationToken)
    {
        var last = lines.Where(l => l.Kind == RouteKind.DELIVERY).OrderByDescending(l => l.CreatedAt).FirstOrDefault();
        var rows = await db.Couriers.AsNoTracking().Where(c => c.DeletedAt == null && c.IsActive).OrderBy(c => c.FirstName).ThenBy(c => c.LastName).Take(500)
            .Select(c => new { c.Id, c.FirstName, c.LastName, Covers = c.CityId == parcel.DestinationCityId || c.Cities.Any(x => x.CityId == parcel.DestinationCityId) }).ToListAsync(cancellationToken);
        var couriers = rows.Select(r => new CorrectionCourierDto(r.Id, $"{r.FirstName} {r.LastName}".Trim(), r.Covers)).OrderByDescending(c => c.CoversCity).ToList();
        var courierId = !string.IsNullOrWhiteSpace(input.CourierId) ? input.CourierId.Trim() : last?.CourierId;
        if (courierId is null) return (null, true, couriers, []);
        var courier = await db.Couriers.AsNoTracking().Where(c => c.Id == courierId && c.DeletedAt == null).Select(c => new { c.Id, c.IsActive, c.FirstName, c.LastName }).FirstOrDefaultAsync(cancellationToken)
                      ?? throw BusinessException.NotFound("Livreur introuvable.");
        var courierName = $"{courier.FirstName} {courier.LastName}".Trim();
        if (!courier.IsActive) throw BusinessException.Conflict($"{courierName} est désactivé : choisissez un autre livreur.", new { reason = "courier-inactive" });
        var open = await db.Routes.AsNoTracking().Where(r => r.CourierId == courier.Id && r.Kind == RouteKind.DELIVERY && r.Status != RouteStatus.CLOSED).OrderByDescending(r => r.CreatedAt).Take(50)
            .Select(r => new { r.Id, r.Code, r.Status, r.CityId, City = r.City.Name }).ToListAsync(cancellationToken);
        var routes = open.Select(r => new CorrectionRouteDto(r.Code, r.Status.ToString(), r.City)).ToList();
        RouteChoice? route;
        var chosen = input.RouteCode?.Trim();
        if (!string.IsNullOrEmpty(chosen))
        {
            var hit = open.FirstOrDefault(r => r.Code == chosen)
                      ?? throw BusinessException.Validation($"Le circuit {chosen} n’est pas un circuit de livraison ouvert de {courierName}.", new { reason = "route-invalid", routeCode = chosen });
            route = new RouteChoice(hit.Id, hit.Code, hit.Status, hit.CityId);
        }
        else if (last is not null && last.CourierId == courier.Id && last.Status != RouteStatus.CLOSED)
        {
            route = new RouteChoice(last.RouteId, last.Code, last.Status, last.CityId);
        }
        else
        {
            var running = open.Where(r => r.Status is RouteStatus.DISPATCHED or RouteStatus.DELIVERING).ToList();
            var hit = running.FirstOrDefault(r => r.CityId == parcel.DestinationCityId) ?? running.FirstOrDefault() ?? open.FirstOrDefault(r => r.Status == RouteStatus.DRAFT);
            route = hit is null ? null : new RouteChoice(hit.Id, hit.Code, hit.Status, hit.CityId);
        }
        var reuse = route is not null && lines.Any(l => l.RouteId == route.Id);
        return (new Placement(courier.Id, courierName, route, reuse), false, couriers, routes);
    }

    /// <summary>`write` : tout dans la transaction de l'appelant ; rend le circuit d'arrivée (ouvert au besoin).</summary>
    private static async Task<RouteChoice?> WriteAsync(IAppDbContext db, ICurrentUser caller, Plan plan, CorrectionInput input, string reason, string author, DateTime now,
        IReadOnlyList<Dictionary<string, object?>> steps, CancellationToken cancellationToken)
    {
        var route = plan.Placement?.Route;
        if (plan.Placement is not null && route is null)
        {
            var cityId = await db.Couriers.AsNoTracking().Where(c => c.Id == plan.Placement.CourierId).Select(c => c.CityId).FirstOrDefaultAsync(cancellationToken) ?? plan.Parcel.DestinationCityId;
            var opened = await OutForDelivery.OpenDraftAsync(db, caller, plan.Placement.CourierId, cityId, cancellationToken);
            route = new RouteChoice(opened.Id, opened.Code, RouteStatus.DRAFT, opened.CityId);
        }
        var comment = $"Correction du statut : {reason}{(route is not null && plan.Placement is not null ? $" ({plan.Placement.CourierName}, {route.Code})" : string.Empty)}";
        var scheduled = plan.To == RouteStatusIds.Postponed && !string.IsNullOrEmpty(input.ScheduledDate) ? ParcelCreation.ParseDate(input.ScheduledDate) : null;
        var setScheduled = scheduled is not null && !plan.ConfirmOnRoute;
        var courierId = plan.Placement?.CourierId;
        var moved = await db.Parcels.Where(p => p.Id == plan.Parcel.Id && p.StatusId == plan.Parcel.StatusId && p.BillingState == BillingState.TO_INVOICE && p.DeletedAt == null
                                                && (plan.From == RouteStatusIds.Delivered || p.StatusAt == plan.Parcel.StatusAt))
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.StatusId, plan.StatusInTx)
                .SetProperty(p => p.StatusAt, now)
                .SetProperty(p => p.IsFailed, plan.IsFailed)
                .SetProperty(p => p.FailureFee, p => plan.KeepFee ? p.FailureFee : plan.FailureFee == null ? null : Money.FromCents(plan.FailureFee.Value))
                .SetProperty(p => p.ScheduledAt, p => setScheduled ? scheduled : p.ScheduledAt)
                .SetProperty(p => p.CourierId, p => courierId ?? p.CourierId), cancellationToken);
        if (moved == 0) throw BusinessException.Conflict("Le statut de ce colis vient de changer : rechargez la fiche avant de recommencer.", new { reason = "parcel-status-changed", from = plan.Parcel.StatusId });
        foreach (var note in plan.LeaveNotes) await db.DeliveryNoteParcels.Where(l => l.DeliveryNoteId == note.Id && l.ParcelId == plan.Parcel.Id).ExecuteDeleteAsync(cancellationToken);
        if (plan.LeaveHandovers.Count > 0)
        {
            var ids = plan.LeaveHandovers.Select(i => i.Id).ToList();
            await db.ReturnHandoverItems.Where(i => ids.Contains(i.Id)).ExecuteDeleteAsync(cancellationToken);
        }
        if (plan.ReleaseHandovers.Count > 0)
        {
            var ids = plan.ReleaseHandovers.Select(i => i.Id).ToList();
            await db.ReturnHandoverItems.Where(i => ids.Contains(i.Id)).ExecuteUpdateAsync(s => s
                .SetProperty(i => i.ReservedParcelId, (string?)null).SetProperty(i => i.ConfirmedParcelId, (string?)null)
                .SetProperty(i => i.CorrectedAt, now).SetProperty(i => i.CorrectedStatusId, plan.To), cancellationToken);
        }
        if (plan.LeaveRoutes.Count > 0)
        {
            var ids = plan.LeaveRoutes.Select(l => l.Id).ToList();
            await db.RouteParcels.Where(l => l.ParcelId == plan.Parcel.Id && ids.Contains(l.RouteId) && l.Route.Status != RouteStatus.CLOSED).ExecuteDeleteAsync(cancellationToken);
        }
        if (plan.MarkRoutes.Count > 0)
        {
            var ids = plan.MarkRoutes.Select(l => l.Id).ToList();
            await db.RouteParcels.Where(l => l.ParcelId == plan.Parcel.Id && ids.Contains(l.RouteId) && l.CorrectedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.CorrectedAt, now).SetProperty(l => l.CorrectedStatusId, plan.To), cancellationToken);
        }
        if (plan.MarkNotes.Count > 0)
        {
            var ids = plan.MarkNotes.Select(n => n.Id).ToList();
            await db.DeliveryNoteParcels.Where(l => l.ParcelId == plan.Parcel.Id && ids.Contains(l.DeliveryNoteId) && l.CorrectedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.CorrectedAt, now).SetProperty(l => l.CorrectedStatusId, plan.To), cancellationToken);
        }
        if (plan.Placement is not null && route is not null)
        {
            if (plan.Placement.Reuse)
            {
                await db.RouteParcels.Where(l => l.RouteId == route.Id && l.ParcelId == plan.Parcel.Id).ExecuteUpdateAsync(s => s
                    .SetProperty(l => l.Confirmed, false).SetProperty(l => l.ConfirmedAt, (DateTime?)null).SetProperty(l => l.ConfirmedBy, (string?)null)
                    .SetProperty(l => l.FinalStatusId, (int?)null), cancellationToken);
            }
            else
            {
                var last = await db.RouteParcels.Where(l => l.RouteId == route.Id).MaxAsync(l => (int?)l.Sequence, cancellationToken) ?? 0;
                db.RouteParcels.Add(new RouteParcel { RouteId = route.Id, ParcelId = plan.Parcel.Id, Sequence = last + 1 });
            }
        }
        if (plan.InvoiceDrafts.Count > 0) await InvoiceDrafts.DetachDraftParcelAsync(db, plan.Parcel.Id, cancellationToken);
        db.ParcelEvents.Add(new ParcelEvent
        {
            ParcelId = plan.Parcel.Id, StatusId = plan.StatusInTx, Comment = comment, AuthorId = caller.UserId, AuthorName = author, CityId = route?.CityId ?? plan.Parcel.CurrentCityId,
            ScheduledDate = plan.ConfirmOnRoute ? null : scheduled, CreatedAt = now,
        });
        db.AuditLogs.Add(new AuditLog
        {
            Entity = "Parcel", EntityId = plan.Parcel.Id, Action = AuditAction.UPDATE,
            Before = JsonSerializer.Serialize(new { statusId = plan.From, isFailed = plan.Parcel.IsFailed, failureFee = Money.ToCentsOrNull(plan.Parcel.FailureFee), courierId = plan.Parcel.CourierId }),
            After = JsonSerializer.Serialize(new
            {
                statusId = plan.To, statusCorrection = true, reason, courierId = plan.Placement?.CourierId ?? plan.Parcel.CourierId, routeCode = route?.Code, steps, summary = comment,
            }, JsonSerializerOptions.Web),
            Diff = ["statusId"], UserId = caller.UserId, At = now,
        });
        await db.SaveChangesAsync(cancellationToken);
        return route;
    }

    private static async Task SettleAsync(IAppDbContext db, ICurrentUser caller, ILogger logger, Plan plan, RouteChoice? route, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var leave in plan.LeaveRoutes.Where(l => l.Status != RouteStatus.DRAFT))
            {
                if (leave.Kind == RouteKind.DELIVERY) await RouteClosing.AutoCloseDeliveryAsync(db, caller, logger, leave.Id, cancellationToken);
                if (leave.Kind == RouteKind.RETURN) await RouteClosing.AutoCloseReturnAsync(db, caller, logger, leave.Id, cancellationToken);
            }
            if (plan.Placement is { Reuse: false } && route is not null)
            {
                await Notifier.NotifyCourierAsync(db, plan.Placement.CourierId, "parcel-transferred-in", new Dictionary<string, object> { ["count"] = 1, ["code"] = plan.Parcel.Code, ["route"] = route.Code },
                    new Dictionary<string, string> { ["routeCode"] = route.Code }, cancellationToken);
            }
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("Suites de la correction de {Code} non jouées : {Message}", plan.Parcel.Code, error.Message);
        }
    }

    /// <summary>`assertMoneyFree` : ni remboursé, ni facturé, ni versé, ni remis en caisse ; pas d'argent sans livraison.</summary>
    private static async Task AssertMoneyFreeAsync(IAppDbContext db, ParcelRow parcel, CancellationToken cancellationToken)
    {
        if (parcel.FinancialStatus == FinancialStatus.REMBOURSE || parcel.BillingState == BillingState.REIMBURSED)
        {
            throw BusinessException.Conflict("Ce colis est remboursé au vendeur : son statut ne se corrige plus.", new { reason = "parcel-reimbursed" });
        }
        if (parcel.BillingState != BillingState.TO_INVOICE) throw BusinessException.Conflict("Ce colis est déjà facturé : son statut ne se corrige plus (passez par un avoir).", new { reason = "parcel-invoiced" });
        var payout = await db.CodPayoutParcels.AsNoTracking().Where(l => l.ParcelId == parcel.Id).Select(l => new { l.Payout.Code, l.Payout.Status }).FirstOrDefaultAsync(cancellationToken);
        if (payout is not null)
        {
            throw BusinessException.Conflict($"Le COD de ce colis est sur le bon de versement {payout.Code}{(payout.Status == CodPayoutStatus.PAID ? ", payé" : string.Empty)} : son statut ne se corrige plus.",
                new { reason = "parcel-paid-out", payoutCode = payout.Code });
        }
        var remitted = await db.CashRemittanceParcels.AsNoTracking().Where(l => l.ParcelId == parcel.Id && l.Remittance.Status == CashRemittanceStatus.CONFIRMED).Select(l => l.Remittance.Code).FirstOrDefaultAsync(cancellationToken);
        if (remitted is not null || parcel.ReimbursedByCourier || parcel.FinancialStatus is FinancialStatus.REMIS_LIVREUR or FinancialStatus.FACTURE)
        {
            throw BusinessException.Conflict($"La caisse de ce colis est déjà reçue par l’agence{(remitted is null ? string.Empty : $" ({remitted})")} : son statut ne se corrige plus.",
                new { reason = "parcel-remitted", remittanceCode = remitted });
        }
        var drafted = await db.CashRemittanceParcels.AsNoTracking().Where(l => l.ParcelId == parcel.Id && l.Remittance.Status == CashRemittanceStatus.DRAFT).Select(l => l.Remittance.Code).FirstOrDefaultAsync(cancellationToken);
        if (parcel.StatusId != RouteStatusIds.Delivered && (parcel.FinancialStatus != FinancialStatus.NON_ENCAISSE || drafted is not null))
        {
            throw BusinessException.Conflict($"De l’argent est attaché à ce colis{(drafted is null ? string.Empty : $" (caisse {drafted})")} sans qu’il soit livré : son statut ne se corrige pas ici.",
                new { reason = "parcel-money-collected" });
        }
    }
}
