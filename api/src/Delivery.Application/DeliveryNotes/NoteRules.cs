using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Settings;
using Delivery.Application.Invoices;
using Delivery.Application.Parcels;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Pricing;
using Delivery.Application.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.DeliveryNotes;

/// <summary>Les règles des bons de ramassage (BR-) et de dépôt (BD-) : `note-candidates.ts`, `note-shortfall.ts` et `RoutesService`.</summary>
public static class NoteRules
{
    public const string PickupPrefix = "BR";
    public const string DropoffPrefix = "BD";
    public const int AwaitingPickup = 1;
    public const int AwaitingArrival = 2;
    public const int PickedUp = 3;
    public const int PickupInProgress = 17;
    public const int PickedUpByCourier = 18;
    public static readonly int[] CandidateStatusIds = [AwaitingPickup, AwaitingArrival];
    public static readonly DeliveryNoteStatus[] ActiveStatuses = [DeliveryNoteStatus.AWAITING_VALIDATION, DeliveryNoteStatus.PICKED_UP_BY_COURIER];
    public static readonly int[] PickupSourceStatusIds = [1, 2, 17, 18, 38];
    public static readonly int[] RemovableFromNote = [1, 17, 18, 3];
    public const int SelectionMax = 5_000;
    public const int BulkAsyncThreshold = 300;

    public static int StatusFor(DeliveryNoteType type) => type == DeliveryNoteType.PICKUP ? AwaitingPickup : AwaitingArrival;

    public static string AlignmentComment(DeliveryNoteType type, string noteCode) => type == DeliveryNoteType.PICKUP
        ? $"Mis sur le bon de ramassage {noteCode} : le colis sera ramassé."
        : $"Mis sur le bon de dépôt {noteCode} : le colis sera déposé en agence.";

    public static string RemovalReason(string noteCode) => $"Non remis au ramassage du {noteCode} : reste en attente de ramassage";

    public static string LegacyRemovalReason(string noteCode) => $"Supprimé : non remis au ramassage du {noteCode}";

    public static string RestoreReason(string noteCode) => $"Restauré : retiré par erreur du {noteCode}";

    /// <summary>`effectivePickupPolicy` : les conditions du client l'emportent sur la politique.</summary>
    public static PricingPolicy EffectivePickupPolicy(PricingPolicy policy, decimal? clientPickupFee, int? clientThreshold) =>
        policy with { PickupFee = clientPickupFee is { } fee ? Money.ToCents(fee) : policy.PickupFee, FreePickupThreshold = clientThreshold ?? policy.FreePickupThreshold };

    /// <summary>`noteRule` : le frais et le seuil figés à la validation, sinon ceux du moment.</summary>
    public static (long Fee, long FreeFrom) RuleOf(DeliveryNoteStatus status, decimal? feeApplied, int? thresholdApplied, decimal? clientPickupFee, int? clientThreshold, PricingPolicy policy)
    {
        if (thresholdApplied is { } frozen) return (Money.ToCents(feeApplied), frozen);
        var rule = status == DeliveryNoteStatus.VALIDATED ? policy : EffectivePickupPolicy(policy, clientPickupFee, clientThreshold);
        return (rule.PickupFee, rule.FreePickupThreshold);
    }

    /// <summary>`resolveSelection` : des identifiants, ou « tout » moins des exclus ; 5 000 au plus, au moins un.</summary>
    public static async Task<List<string>> ResolveSelectionAsync(IReadOnlyList<string>? ids, bool? all, IReadOnlyList<string>? exclude, Func<int, Task<List<string>>> findAll)
    {
        List<string> selected;
        if (all == true)
        {
            var excluded = (exclude ?? []).ToHashSet();
            selected = (await findAll(SelectionMax + 1)).Where(id => !excluded.Contains(id)).ToList();
        }
        else selected = (ids ?? []).Distinct().ToList();
        if (selected.Count > SelectionMax)
        {
            throw BusinessException.Validation($"Sélection trop grande : {SelectionMax} éléments au plus par opération.", new { reason = "selection-too-large", limit = SelectionMax });
        }
        if (selected.Count == 0) throw BusinessException.Validation("Aucun élément sélectionné.", new { reason = "selection-empty" });
        return selected;
    }

    /// <summary>`noteCandidateIds` : les colis du vendeur en attente, sur aucun bon actif.</summary>
    public static Task<List<string>> CandidateIdsAsync(IAppDbContext db, string clientId, string? search, int take, CancellationToken cancellationToken)
    {
        var where = ParcelFilters.NoteCandidates(db.Parcels.AsNoTracking().Where(p => p.ClientId == clientId && p.DeletedAt == null));
        if (!string.IsNullOrWhiteSpace(search)) where = ParcelFilters.Search(where, search);
        return where.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id).Select(p => p.Id).Take(take).ToListAsync(cancellationToken);
    }

    /// <summary>`lockFreeOfNotes` : verrou des colis puis contrôle qu'aucun autre bon actif ne les porte.</summary>
    public static async Task LockFreeOfNotesAsync(IAppDbContext db, IReadOnlyList<string> parcelIds, string? exceptNoteId, CancellationToken cancellationToken)
    {
        var ids = parcelIds.ToArray();
        await db.Database.SqlQuery<string>($"SELECT id AS \"Value\" FROM parcels WHERE id = ANY({ids}::text[]) ORDER BY id FOR UPDATE").ToListAsync(cancellationToken);
        var busy = await BusyAsync(db, parcelIds, exceptNoteId, cancellationToken);
        if (busy is not null)
        {
            throw BusinessException.Validation($"Le colis {busy.Value.Parcel} vient d’être mis sur le bon {busy.Value.Note}.", new { reason = "parcel-in-note", code = busy.Value.Parcel, noteCode = busy.Value.Note });
        }
    }

    private static async Task<(string Parcel, string Note)?> BusyAsync(IAppDbContext db, IReadOnlyList<string> parcelIds, string? exceptNoteId, CancellationToken cancellationToken)
    {
        var row = await db.DeliveryNoteParcels.AsNoTracking()
            .Where(l => parcelIds.Contains(l.ParcelId) && (exceptNoteId == null || l.DeliveryNoteId != exceptNoteId) && ActiveStatuses.Contains(l.DeliveryNote.Status))
            .Select(l => new { Parcel = l.Parcel.Code, Note = l.DeliveryNote.Code }).FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : (row.Parcel, row.Note);
    }

    public sealed record NoteParcelRef(string Id, string Code, int StatusId);

    /// <summary>`assertNoteParcels` : même vendeur, en attente, libres d'autre bon, hors circuit s'ils changent de mode.</summary>
    public static async Task<List<NoteParcelRef>> AssertParcelsAsync(IAppDbContext db, string clientId, DeliveryNoteType type, IReadOnlyList<string> parcelIds, string? exceptNoteId, CancellationToken cancellationToken)
    {
        var parcels = await db.Parcels.AsNoTracking().Where(p => parcelIds.Contains(p.Id) && p.DeletedAt == null)
            .Select(p => new { p.Id, p.Code, p.StatusId, p.ClientId }).ToListAsync(cancellationToken);
        if (parcels.Count != parcelIds.Distinct().Count()) throw BusinessException.Validation("Colis inconnu.", new { field = "parcelIds" });
        foreach (var parcel in parcels)
        {
            if (parcel.ClientId != clientId) throw BusinessException.Conflict($"Le colis {parcel.Code} appartient à un autre e-commerçant.", new { reason = "parcel-other-client", code = parcel.Code });
            if (!CandidateStatusIds.Contains(parcel.StatusId))
            {
                throw BusinessException.Conflict($"Le colis {parcel.Code} n'est pas dans l'état attendu pour ce bon.", new { reason = "parcel-wrong-status", code = parcel.Code, statusId = parcel.StatusId });
            }
        }
        var busy = await BusyAsync(db, parcelIds, exceptNoteId, cancellationToken);
        if (busy is not null)
        {
            throw BusinessException.Validation($"Le colis {busy.Value.Parcel} est déjà sur le bon {busy.Value.Note}.", new { reason = "parcel-in-note", code = busy.Value.Parcel, noteCode = busy.Value.Note });
        }
        var target = StatusFor(type);
        var switching = parcels.Where(p => p.StatusId != target).Select(p => p.Id).ToList();
        if (switching.Count > 0)
        {
            var inRoute = await db.RouteParcels.AsNoTracking().Where(l => switching.Contains(l.ParcelId) && l.Route.Status != RouteStatus.CLOSED)
                .Select(l => new { Parcel = l.Parcel.Code, Route = l.Route.Code }).FirstOrDefaultAsync(cancellationToken);
            if (inRoute is not null)
            {
                throw BusinessException.Validation($"Le colis {inRoute.Parcel} est dans le circuit {inRoute.Route} : retirez-l’en avant de changer son mode.", new { reason = "parcel-in-route", code = inRoute.Parcel });
            }
        }
        return parcels.Select(p => new NoteParcelRef(p.Id, p.Code, p.StatusId)).ToList();
    }

    /// <summary>`alignParcelsToNote` : un colis de bon de ramassage attend son ramassage, un de dépôt son arrivée.</summary>
    public static async Task AlignParcelsAsync(IAppDbContext db, ICurrentUser caller, IReadOnlyList<NoteParcelRef> parcels, DeliveryNoteType type, string noteCode, string? cityId, DateTime now, CancellationToken cancellationToken)
    {
        var target = StatusFor(type);
        var moving = parcels.Where(p => p.StatusId != target).Select(p => p.Id).ToList();
        if (moving.Count == 0) return;
        foreach (var parcel in await db.Parcels.Where(p => moving.Contains(p.Id)).ToListAsync(cancellationToken))
        {
            parcel.StatusId = target;
            parcel.StatusAt = now;
            db.ParcelEvents.Add(new ParcelEvent { ParcelId = parcel.Id, StatusId = target, Comment = AlignmentComment(type, noteCode), AuthorId = caller.UserId, CityId = cityId, CreatedAt = now });
        }
    }

    /// <summary>`removeFromNote` : les colis non remis quittent le bon (et leur circuit de ramassage) et repassent en attente.</summary>
    public static async Task<int> RemoveFromNoteAsync(IAppDbContext db, ICurrentUser caller, DeliveryNote note, IReadOnlyList<Parcel> parcels, IReadOnlyList<string> routeIds, string authorName, string? cityId,
        DateTime now, CancellationToken cancellationToken)
    {
        var blocked = parcels.FirstOrDefault(p => !RemovableFromNote.Contains(p.StatusId));
        if (blocked is not null)
        {
            throw BusinessException.Transition($"Le colis {blocked.Code} a déjà quitté le ramassage : il ne se retire plus du bon {note.Code}.",
                new { reason = "parcel-not-removable", code = blocked.Code, statusId = blocked.StatusId });
        }
        var routeId = routeIds.FirstOrDefault();
        var reason = RemovalReason(note.Code);
        foreach (var parcel in parcels)
        {
            db.DeliveryNoteParcels.RemoveRange(await db.DeliveryNoteParcels.Where(l => l.DeliveryNoteId == note.Id && l.ParcelId == parcel.Id).ToListAsync(cancellationToken));
            db.DeliveryNoteRemovedParcels.Add(new DeliveryNoteRemovedParcel { DeliveryNoteId = note.Id, ParcelId = parcel.Id, RouteId = routeId, RemovedAt = now, RemovedBy = caller.UserId });
            db.RouteParcels.RemoveRange(await db.RouteParcels.Where(l => l.ParcelId == parcel.Id
                && (routeIds.Contains(l.RouteId) || (l.Route.Kind == RouteKind.PICKUP && l.Route.Status != RouteStatus.CLOSED))).ToListAsync(cancellationToken));
            parcel.PickupWaived = false;
            if (parcel.StatusId != AwaitingPickup)
            {
                parcel.StatusId = AwaitingPickup;
                parcel.StatusAt = now;
                db.ParcelEvents.Add(new ParcelEvent { ParcelId = parcel.Id, StatusId = AwaitingPickup, Comment = reason, AuthorId = caller.UserId, AuthorName = authorName, CityId = cityId, CreatedAt = now });
            }
            ParcelJournal.Record(db, caller, parcel.Id, ParcelJournal.NoteDepartureField, note.Code, string.Empty, reason, authorName, now);
        }
        return parcels.Count;
    }

    /// <summary>
    /// `applyNoteValidation` : les colis ramassés passent en « Colis ramassé », le ramassage est offert au-delà
    /// du seuil, sinon son frais part sur la facture brouillon du client (une seule fois par bon).
    /// </summary>
    public static async Task ApplyValidationAsync(IAppDbContext db, ICurrentUser caller, DeliveryNote note, IReadOnlyList<Parcel> parcels, PricingPolicy globalPolicy, DateTime now,
        CancellationToken cancellationToken, string? comment = null, bool parcelsOnly = false, bool noteOnly = false, int? pickedUpCount = null)
    {
        var pickedUp = parcels.Where(p => p.StatusId == PickedUp || PickupSourceStatusIds.Contains(p.StatusId)).ToList();
        var count = pickedUpCount ?? pickedUp.Count;
        var client = await db.Clients.AsNoTracking().Where(c => c.Id == note.ClientId).Select(c => new { c.PickupFee, c.FreePickupThreshold }).FirstOrDefaultAsync(cancellationToken);
        var policy = EffectivePickupPolicy(globalPolicy, client?.PickupFee, client?.FreePickupThreshold);
        var waive = note.Type == DeliveryNoteType.PICKUP && count >= policy.FreePickupThreshold;
        var text = comment ?? $"Validation du bon {note.Code}";
        var author = await ParcelStatusChange.AuthorNameAsync(db, caller.UserId, cancellationToken);
        if (!noteOnly)
        {
            foreach (var parcel in pickedUp)
            {
                if (waive && !parcel.PickupWaived) parcel.PickupWaived = true;
                if (parcel.StatusId == PickedUp) continue;
                parcel.StatusId = PickedUp;
                parcel.StatusAt = now;
                db.ParcelEvents.Add(new ParcelEvent { ParcelId = parcel.Id, StatusId = PickedUp, Comment = text, AuthorId = caller.UserId, AuthorName = author, CityId = note.LocationCityId, CreatedAt = now });
            }
        }
        if (parcelsOnly)
        {
            await db.SaveChangesAsync(cancellationToken);
            return;
        }
        var firstPickupValidation = note.Type == DeliveryNoteType.PICKUP && note.Status != DeliveryNoteStatus.VALIDATED;
        var charged = firstPickupValidation && count > 0 && !waive && policy.PickupFee > 0 ? policy.PickupFee : 0;
        note.Status = DeliveryNoteStatus.VALIDATED;
        note.ValidatedAt = now;
        note.ValidatedBy = caller.UserId;
        if (firstPickupValidation)
        {
            note.PickupFeeApplied = Money.FromCents(charged);
            note.PickupThresholdApplied = (int)policy.FreePickupThreshold;
        }
        await db.SaveChangesAsync(cancellationToken);
        if (charged > 0) await InvoiceDrafts.ChargePickupNoteAsync(db, caller, note.Id, note.Code, note.ClientId, note.AgencyId, count, charged, now, cancellationToken);
    }

    public static Task<List<string>> OpenRoutesOfNoteAsync(IAppDbContext db, string noteId, CancellationToken cancellationToken) =>
        db.RouteNotes.AsNoTracking().Where(l => l.DeliveryNoteId == noteId && l.Route.Status != RouteStatus.CLOSED).Select(l => l.RouteId).ToListAsync(cancellationToken);

    public static async Task<PricingPolicy> PolicyAsync(IAppDbContext db, string? agencyId, CancellationToken cancellationToken) => await db.PricingPolicyAsync(cancellationToken, agencyId);

    /// <summary>`announceRemoval` : le vendeur apprend combien de colis ont quitté son bon (best effort).</summary>
    public static async Task AnnounceRemovalAsync(IAppDbContext db, ILogger logger, DeliveryNote note, int removed, CancellationToken cancellationToken)
    {
        try
        {
            var kept = await db.DeliveryNoteParcels.CountAsync(l => l.DeliveryNoteId == note.Id, cancellationToken);
            var removedTotal = await db.DeliveryNoteRemovedParcels.CountAsync(r => r.DeliveryNoteId == note.Id, cancellationToken);
            await Notifier.NotifyMerchantAsync(db, note.ClientId, "note-parcels-removed",
                new Dictionary<string, object> { ["removed"] = removed, ["picked"] = kept, ["total"] = kept + removedTotal, ["code"] = note.Code },
                new Dictionary<string, string> { ["noteCode"] = note.Code }, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("Notification « note-parcels-removed » du bon {Code} non envoyée : {Message}", note.Code, error.Message);
        }
    }
}
