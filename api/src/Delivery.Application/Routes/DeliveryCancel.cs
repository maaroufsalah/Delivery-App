using System.Text.Json;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Finance;
using Delivery.Application.Invoices;
using Delivery.Application.Notifications;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Routes;

public sealed record CancelDeliveryResultDto(string Code, int StatusId, string? RouteCode, bool RouteReopened, string? RemittanceCode, bool SlipRemoved);

/// <summary>
/// `DeliveryCancelService` : défaire une livraison — statut, encaissement, commission, ligne du RF- brouillon, facture
/// brouillon, chèque encore chez le livreur, BL collecté, et le circuit clos automatiquement se rouvre. Le plan se lit
/// hors transaction ; l'écriture se joue dans la transaction de l'appelant (la correction de statut y enchaîne la sienne).
/// </summary>
public static class DeliveryCancel
{
    public const int AwaitingDelivery = 16;

    public sealed record RouteLink(string RouteId, string Code, RouteStatus Status, string CourierId);

    public sealed record Plan(string ParcelId, string Code, string ClientId, string? CourierId, string? CurrentCityId, RouteLink? Route, string? ChequeId, ChequeStatus? ChequeStatus,
        string? SlipId, bool Reopen, RouteStatus? PreviousRouteStatus, int Target, string? RemittanceCode);

    public static void AssertAdmin(ICurrentUser caller)
    {
        if (!caller.IsSuperAdmin()) throw BusinessException.Forbidden("Seul un administrateur annule une livraison.", new { reason = "cancel-delivery-admin-only" });
    }

    /// <summary>`cancelDelivery` jusqu'à la transaction, et `previewCancel` : tout ce qui refuse, et ce que l'annulation fera.</summary>
    public static async Task<Plan> PlanAsync(IAppDbContext db, string code, CancellationToken cancellationToken)
    {
        var parcel = await db.Parcels.AsNoTracking().Where(p => p.Code == code && p.DeletedAt == null)
                         .Select(p => new { p.Id, p.Code, p.StatusId, p.BillingState, p.ReimbursedByCourier, p.ClientId, p.CourierId, p.CurrentCityId }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound($"Colis introuvable : {code}");
        if (parcel.StatusId != RouteStatusIds.Delivered)
        {
            throw BusinessException.Conflict("Ce colis n’est pas livré : il n’y a pas de livraison à annuler.", new { reason = "parcel-not-delivered", statusId = parcel.StatusId });
        }
        await AssertMoneyStillOpenAsync(db, parcel.Id, parcel.BillingState, parcel.ReimbursedByCourier, cancellationToken);
        var link = await db.RouteParcels.AsNoTracking().Where(l => l.ParcelId == parcel.Id && l.FinalStatusId == RouteStatusIds.Delivered && l.Route.Kind == RouteKind.DELIVERY)
            .OrderByDescending(l => l.ConfirmedAt).Select(l => new RouteLink(l.RouteId, l.Route.Code, l.Route.Status, l.Route.CourierId)).FirstOrDefaultAsync(cancellationToken);
        var cheque = await db.Cheques.AsNoTracking().Where(c => c.ParcelId == parcel.Id).Select(c => new { c.Id, c.Status }).FirstOrDefaultAsync(cancellationToken);
        if (cheque is not null && cheque.Status is not (ChequeStatus.RECU_LIVREUR or ChequeStatus.ANNULE or ChequeStatus.REJETE))
        {
            throw BusinessException.Conflict("Le chèque de ce colis a déjà été remis à l’agence : sa livraison ne s’annule plus ici. Annulez d’abord le chèque.", new { reason = "cheque-handed-over" });
        }
        var slip = await db.DeliverySlips.AsNoTracking().Where(s => s.ParcelId == parcel.Id)
            .Select(s => new { s.Id, s.Status, Carrier = s.NoteItems.Where(i => i.Note.Status != BlNoteStatus.DRAFT).Select(i => i.Note.Code).FirstOrDefault() }).FirstOrDefaultAsync(cancellationToken);
        if (slip is not null && (slip.Status != DeliverySlipStatus.COLLECTED || slip.Carrier is not null))
        {
            throw BusinessException.Conflict(slip.Carrier is not null
                    ? $"Le BL de ce colis est sur le bon {slip.Carrier} : il suit son bon. Reculez d’abord le BL."
                    : "Le BL de ce colis est déjà réceptionné : reculez-le d’abord (Bons de livraison).",
                new { reason = "bl-already-received", noteCode = slip.Carrier });
        }
        RouteStatus? previous = link is { Status: RouteStatus.CLOSED } ? await AutomaticClosureAsync(db, link.RouteId, cancellationToken) : null;
        var reopen = link is { Status: RouteStatus.CLOSED } && previous is not null;
        var backOnRoute = link is not null && (link.Status != RouteStatus.CLOSED || reopen);
        var target = backOnRoute ? await StatusBeforeDeliveryAsync(db, parcel.Id, cancellationToken) : AwaitingDelivery;
        var draft = await db.CashRemittanceParcels.AsNoTracking().Where(l => l.ParcelId == parcel.Id && l.ConfirmedParcelId == null && l.Remittance.Status == CashRemittanceStatus.DRAFT)
            .Select(l => l.Remittance.Code).FirstOrDefaultAsync(cancellationToken);
        return new Plan(parcel.Id, parcel.Code, parcel.ClientId, parcel.CourierId, parcel.CurrentCityId, link, cheque?.Id, cheque?.Status, slip?.Id, reopen, previous, target, draft);
    }

    /// <summary>Les écritures, dans la transaction ouverte par l'appelant. Rend le code du RF- d'où la ligne est sortie.</summary>
    public static async Task<string?> WriteAsync(IAppDbContext db, ICurrentUser caller, Plan plan, string reason, string author, DateTime now, CancellationToken cancellationToken)
    {
        var moved = await db.Parcels.Where(p => p.Id == plan.ParcelId && p.StatusId == RouteStatusIds.Delivered && p.BillingState == BillingState.TO_INVOICE && p.DeletedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.StatusId, plan.Target)
                .SetProperty(p => p.StatusAt, now)
                .SetProperty(p => p.FinancialStatus, FinancialStatus.NON_ENCAISSE)
                .SetProperty(p => p.CourierCommission, (decimal?)null)
                .SetProperty(p => p.CourierCommissionAt, (DateTime?)null)
                .SetProperty(p => p.CourierCommissionSource, (CourierCommissionSource?)null)
                .SetProperty(p => p.CourierCommissionCourierId, (string?)null), cancellationToken);
        if (moved == 0) throw BusinessException.Conflict("Le statut de ce colis vient de changer : rechargez la fiche avant de recommencer.", new { reason = "parcel-status-changed", from = RouteStatusIds.Delivered });
        if (plan.Route is { } route)
        {
            await db.RouteParcels.Where(l => l.RouteId == route.RouteId && l.ParcelId == plan.ParcelId).ExecuteUpdateAsync(s => s
                .SetProperty(l => l.Confirmed, false)
                .SetProperty(l => l.ConfirmedAt, (DateTime?)null)
                .SetProperty(l => l.ConfirmedBy, (string?)null)
                .SetProperty(l => l.FinalStatusId, (int?)null)
                .SetProperty(l => l.CodCollected, (decimal?)null)
                .SetProperty(l => l.CodCash, (decimal?)null)
                .SetProperty(l => l.CodCheque, (decimal?)null)
                .SetProperty(l => l.CourierCommission, (decimal?)null)
                .SetProperty(l => l.CourierCommissionSource, (CourierCommissionSource?)null), cancellationToken);
        }
        var remittances = await RemittanceLines.DetachDeliveredAsync(db, plan.ParcelId, cancellationToken);
        await InvoiceDrafts.DetachDraftParcelAsync(db, plan.ParcelId, cancellationToken);
        if (plan.ChequeStatus == ChequeStatus.RECU_LIVREUR)
        {
            await db.ChequeEvents.Where(e => e.ChequeId == plan.ChequeId).ExecuteDeleteAsync(cancellationToken);
            await db.Cheques.Where(c => c.Id == plan.ChequeId && c.Status == ChequeStatus.RECU_LIVREUR).ExecuteDeleteAsync(cancellationToken);
        }
        if (plan.SlipId is not null)
        {
            await db.BlNoteItems.Where(i => i.SlipId == plan.SlipId && i.Note.Status == BlNoteStatus.DRAFT).ExecuteDeleteAsync(cancellationToken);
            await db.DeliverySlips.Where(s => s.Id == plan.SlipId && s.Status == DeliverySlipStatus.COLLECTED).ExecuteDeleteAsync(cancellationToken);
        }
        var comment = $"Livraison annulée par l’admin — {reason}";
        if (plan.Reopen && plan.Route is { } closed && plan.PreviousRouteStatus is { } previous)
        {
            await db.Routes.Where(r => r.Id == closed.RouteId && r.Status == RouteStatus.CLOSED)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, previous).SetProperty(r => r.StatusAt, now), cancellationToken);
            db.AuditLogs.Add(new AuditLog
            {
                Entity = "Route", EntityId = closed.RouteId, Action = AuditAction.UPDATE, Before = JsonSerializer.Serialize(new { status = "CLOSED" }),
                After = JsonSerializer.Serialize(new { status = previous.ToString(), reopened = true, summary = $"Rouvert : livraison de {plan.Code} annulée par l’admin" }),
                Diff = ["status"], UserId = caller.UserId, At = now,
            });
        }
        db.ParcelEvents.Add(new ParcelEvent { ParcelId = plan.ParcelId, StatusId = plan.Target, Comment = comment, AuthorId = caller.UserId, AuthorName = author, CityId = plan.CurrentCityId, IsDispute = true, CreatedAt = now });
        db.AuditLogs.Add(new AuditLog
        {
            Entity = "Parcel", EntityId = plan.ParcelId, Action = AuditAction.UPDATE, Before = JsonSerializer.Serialize(new { statusId = RouteStatusIds.Delivered }),
            After = JsonSerializer.Serialize(new { statusId = plan.Target, deliveryCancelled = true, summary = $"{comment}{(plan.Route is null ? string.Empty : $" (circuit {plan.Route.Code})")}" }),
            Diff = ["statusId", "financialStatus"], UserId = caller.UserId, At = now,
        });
        await db.SaveChangesAsync(cancellationToken);
        return remittances.FirstOrDefault();
    }

    /// <summary>Après validation : livreur et vendeur prévenus (sans faire échouer l'annulation).</summary>
    public static async Task SettleAsync(IAppDbContext db, ILogger logger, Plan plan, CancellationToken cancellationToken)
    {
        // Relevés livreur brouillons à recalculer : branché avec le module « Paie des livreurs » (aucun relevé n'existe avant).
        try
        {
            if ((plan.Route?.CourierId ?? plan.CourierId) is { } courierId)
            {
                await Notifier.NotifyCourierAsync(db, courierId, "delivery-cancelled", new Dictionary<string, object> { ["code"] = plan.Code },
                    new Dictionary<string, string> { ["parcelCode"] = plan.Code }, cancellationToken);
            }
            await Notifier.NotifyMerchantAsync(db, plan.ClientId, "parcel-delivery-cancelled", new Dictionary<string, object> { ["code"] = plan.Code },
                new Dictionary<string, string> { ["parcelCode"] = plan.Code }, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("Notifications de l’annulation de {Code} non envoyées : {Message}", plan.Code, error.Message);
        }
    }

    /// <summary>`assertMoneyStillOpen` : ni facturé, ni remis en caisse, ni versé, ni payé au livreur, ni échange constaté.</summary>
    public static async Task AssertMoneyStillOpenAsync(IAppDbContext db, string parcelId, BillingState billingState, bool reimbursedByCourier, CancellationToken cancellationToken)
    {
        const string adjust = "Corrigez par une ligne d’ajustement sur le brouillon de facture du vendeur.";
        if (billingState != BillingState.TO_INVOICE)
        {
            var at = await db.InvoiceLines.AsNoTracking().Where(l => l.ParcelId == parcelId && l.InvoicedParcelId != null).Select(l => l.Invoice.Code).FirstOrDefaultAsync(cancellationToken);
            throw BusinessException.Conflict($"Ce colis est déjà facturé{(at is null ? string.Empty : $" ({at})")} : sa livraison ne s’annule plus. {adjust}", new { reason = "parcel-invoiced", invoiceCode = at });
        }
        var remitted = await db.CashRemittanceParcels.AsNoTracking().Where(l => l.ParcelId == parcelId && l.Remittance.Status == CashRemittanceStatus.CONFIRMED).Select(l => l.Remittance.Code).FirstOrDefaultAsync(cancellationToken);
        if (remitted is not null || reimbursedByCourier)
        {
            throw BusinessException.Conflict($"Ce colis est déjà remis en caisse{(remitted is null ? string.Empty : $" ({remitted})")} : sa livraison ne s’annule plus. {adjust}",
                new { reason = "parcel-remitted", remittanceCode = remitted });
        }
        var paidOut = await db.CodPayoutParcels.AsNoTracking().Where(l => l.ParcelId == parcelId).Select(l => l.Payout.Code).FirstOrDefaultAsync(cancellationToken);
        if (paidOut is not null)
        {
            throw BusinessException.Conflict($"Le COD de ce colis est sur le versement {paidOut} : sa livraison ne s’annule plus. {adjust}", new { reason = "parcel-paid-out", payoutCode = paidOut });
        }
        var settled = await db.CourierStatementLines.AsNoTracking().Where(l => l.ParcelId == parcelId && l.Kind == CourierStatementLineKind.DELIVERY && l.Statement.Status != CourierStatementStatus.DRAFT)
            .Select(l => l.Statement.Code).FirstOrDefaultAsync(cancellationToken);
        if (settled is not null)
        {
            throw BusinessException.Conflict($"La course de ce colis est payée au livreur (relevé {settled}) : sa livraison ne s’annule plus.", new { reason = "parcel-on-statement", statementCode = settled });
        }
        var exchange = await db.Exchanges.AsNoTracking()
            .Where(e => (e.NewParcelId == parcelId || e.OriginalParcelId == parcelId) && (e.Status == ExchangeStatus.NOUVEAU_LIVRE || e.Status == ExchangeStatus.CLOTURE))
            .Select(e => e.Code).FirstOrDefaultAsync(cancellationToken);
        if (exchange is not null)
        {
            throw BusinessException.Conflict($"L’échange {exchange} est déjà constaté livré : annulez d’abord l’échange.", new { reason = "exchange-delivered", exchangeCode = exchange });
        }
    }

    /// <summary>`automaticClosure` : le circuit a-t-il été clos automatiquement, et depuis quel statut.</summary>
    private static async Task<RouteStatus?> AutomaticClosureAsync(IAppDbContext db, string routeId, CancellationToken cancellationToken)
    {
        var rows = await db.Database.SqlQuery<string?>(
            $"""SELECT "before"::text AS "Value" FROM audit_logs WHERE entity = 'Route' AND "entityId" = {routeId} AND ("after"->>'automatic') = 'true' ORDER BY "at" DESC LIMIT 1""")
            .ToListAsync(cancellationToken);
        if (rows.Count == 0) return null;
        var before = rows[0] is { } json ? JsonDocument.Parse(json).RootElement : default;
        var status = before.ValueKind == JsonValueKind.Object && before.TryGetProperty("status", out var s) ? s.GetString() : null;
        return status == "DELIVERING" ? RouteStatus.DELIVERING : RouteStatus.DISPATCHED;
    }

    /// <summary>`statusBeforeDelivery` : l'issue d'avant « Livré » si le livreur pouvait en repartir, sinon « En cours de livraison ».</summary>
    private static async Task<int> StatusBeforeDeliveryAsync(IAppDbContext db, string parcelId, CancellationToken cancellationToken)
    {
        var events = await db.ParcelEvents.AsNoTracking().Where(e => e.ParcelId == parcelId).OrderByDescending(e => e.CreatedAt).Take(30).Select(e => e.StatusId).ToListAsync(cancellationToken);
        var delivered = events.IndexOf(RouteStatusIds.Delivered);
        int? before = delivered < 0 ? null : events.Skip(delivered + 1).Cast<int?>().FirstOrDefault(id => id != RouteStatusIds.Delivered);
        return before is { } id2 && StatusCatalog.CourierSourceStatusIds.Contains(id2) && StatusCatalog.CanTransition(id2, RouteStatusIds.Delivered) ? id2 : RouteStatusIds.OutForDelivery;
    }
}
