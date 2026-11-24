using System.Globalization;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Returns;

/// <summary>`ReturnsService` côté bons de retour livreur (RL-).</summary>
public static class Handovers
{
    public sealed record Editable(string Id, string Code, string CourierId, ReturnHandoverOrigin Origin);

    public static IQueryable<ReturnHandoverListItemDto> Project(IQueryable<ReturnHandover> handovers) => handovers.Select(h => new ReturnHandoverListItemDto
    {
        Id = h.Id, Code = h.Code,
        Status = h.Status == ReturnHandoverStatus.DRAFT ? "draft" : h.Status == ReturnHandoverStatus.RETURN_REQUESTED ? "return-requested" : h.Status == ReturnHandoverStatus.PICKED_UP ? "picked-up"
            : h.Status == ReturnHandoverStatus.AWAITING_RECEIPT ? "awaiting-receipt" : h.Status == ReturnHandoverStatus.CONFIRMED ? "confirmed" : h.Status == ReturnHandoverStatus.CLOSED ? "closed" : "cancelled",
        Origin = h.Origin == ReturnHandoverOrigin.ADMIN ? "admin" : "courier",
        CourierCanEdit = h.CourierCanEdit, CourierId = h.CourierId, CourierName = h.Courier.FirstName + " " + h.Courier.LastName, AgencyId = h.AgencyId, AgencyName = h.Agency.Name,
        ParcelCount = h.Items.Count, ClientNames = h.Items.Select(i => i.Parcel.Client.Name).Distinct().ToList(),
        CreatedByName = h.CreatedBy != null ? h.CreatedBy.FirstName + " " + h.CreatedBy.LastName : null, PickedUpAt = h.PickedUpAt, HandedOverAt = h.HandedOverAt, ConfirmedAt = h.ConfirmedAt,
        ConfirmedByName = h.ConfirmedBy != null ? h.ConfirmedBy.FirstName + " " + h.ConfirmedBy.LastName : null, ClosedAt = h.ClosedAt, CancelledAt = h.CancelledAt, CreatedAt = h.CreatedAt,
    });

    private static T Sorted<T>(T row) where T : ReturnHandoverListItemDto
    {
        row.ClientNames = row.ClientNames.Order(StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.None)).ToList();
        return row;
    }

    public static async Task<List<ReturnHandoverListItemDto>> ListAsync(IQueryable<ReturnHandover> handovers, CancellationToken cancellationToken) =>
        (await Project(handovers).ToListAsync(cancellationToken)).Select(Sorted).ToList();

    public static bool CanEdit(ICurrentUser caller, string status, string origin, bool courierCanEdit) =>
        ReturnRows.IsEditable(status) && (caller.CourierId is null || origin == "courier" || courierCanEdit);

    public static async Task<ReturnHandoverDetailDto> DetailAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken)
    {
        var scoped = ReturnRows.Handovers(db.ReturnHandovers.AsNoTracking(), caller).Where(h => h.Code == code);
        var row = await Project(scoped).FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound($"Bon de retour introuvable : {code}");
        Sorted(row);
        var extra = await scoped.Select(h => new { h.Note, h.CancelReason }).FirstAsync(cancellationToken);
        var items = await db.ReturnHandoverItems.AsNoTracking().Where(i => i.HandoverId == row.Id).OrderBy(i => i.CreatedAt)
            .Select(i => new ReturnHandoverItemDto(i.Id, i.ParcelId, i.Parcel.Code, i.Parcel.Client.Name, i.Parcel.RecipientName, i.Parcel.StatusId, i.Note, i.Parcel.CreatedAt, i.Parcel.Quantity,
                i.CorrectedAt)).ToListAsync(cancellationToken);
        var steps = await db.ReturnHandoverEvents.AsNoTracking().Where(e => e.HandoverId == row.Id).OrderBy(e => e.CreatedAt)
            .Select(e => new { e.Id, e.Status, e.Comment, Author = e.Author != null ? e.Author.FirstName + " " + e.Author.LastName : null, e.CreatedAt }).ToListAsync(cancellationToken);
        var hasParcels = items.Count > 0;
        var status = row.Status;
        var agency = caller.CourierId is null;
        return new ReturnHandoverDetailDto
        {
            Id = row.Id, Code = row.Code, Status = status, Origin = row.Origin, CourierCanEdit = row.CourierCanEdit, CourierId = row.CourierId, CourierName = row.CourierName, AgencyId = row.AgencyId,
            AgencyName = row.AgencyName, ParcelCount = row.ParcelCount, ClientNames = row.ClientNames, CreatedByName = row.CreatedByName, PickedUpAt = row.PickedUpAt, HandedOverAt = row.HandedOverAt,
            ConfirmedAt = row.ConfirmedAt, ConfirmedByName = row.ConfirmedByName, ClosedAt = row.ClosedAt, CancelledAt = row.CancelledAt, CreatedAt = row.CreatedAt,
            Note = extra.Note, CancelReason = extra.CancelReason,
            Editable = CanEdit(caller, status, row.Origin, row.CourierCanEdit),
            Requestable = status == "draft" && hasParcels && agency,
            Pickable = row.Origin == "admin" && status == "return-requested" && hasParcels,
            Handoverable = status == "picked-up" && hasParcels,
            Confirmable = agency && (ReturnRows.IsEditable(status) || status == "awaiting-receipt") && hasParcels,
            Closable = agency && status == "confirmed",
            Cancellable = ReturnRows.IsCancellable(status),
            Deletable = status == "draft",
            Items = items,
            Steps = steps.Select(e => new ReturnHandoverStepDto(e.Id, Money.ToWire(e.Status), e.Comment, e.Author, e.CreatedAt)).ToList(),
        };
    }

    /// <summary>`handoverCandidates` : les colis à rapporter par ce livreur, sur aucun bon.</summary>
    public static async Task<List<ReturnParcelLineDto>> CandidatesAsync(IAppDbContext db, ICurrentUser caller, string? requested, string? search, CancellationToken cancellationToken)
    {
        var courierId = caller.CourierId ?? requested;
        if (string.IsNullOrEmpty(courierId)) throw BusinessException.Validation("Le livreur est obligatoire.", new { field = "courierId" });
        if (!await db.Couriers.AnyAsync(c => c.Id == courierId && c.DeletedAt == null, cancellationToken)) throw BusinessException.NotFound("Livreur introuvable.");
        var where = ReturnRows.HandoverEligible(db.Parcels.AsNoTracking().Where(p => p.CourierId == courierId && p.DeletedAt == null));
        if (!string.IsNullOrWhiteSpace(search)) where = ParcelFilters.Search(where, search);
        var ordered = caller.CourierId is not null ? where.OrderBy(p => p.StatusAt).ThenBy(p => p.Code) : where.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Code);
        return await ReturnRows.Lines(ordered.Take(NoteRules.SelectionMax + 1)).ToListAsync(cancellationToken);
    }

    public static async Task<Editable> LoadEditableAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken)
    {
        var handover = await ReturnRows.Handovers(db.ReturnHandovers.AsNoTracking(), caller).Where(h => h.Code == code)
                           .Select(h => new { h.Id, h.Code, h.CourierId, h.Status, h.Origin, h.CourierCanEdit }).FirstOrDefaultAsync(cancellationToken)
                       ?? throw BusinessException.NotFound($"Bon de retour introuvable : {code}");
        var status = Money.ToWire(handover.Status);
        if (!ReturnRows.IsEditable(status)) throw BusinessException.Conflict("Ce bon ne se modifie plus.", new { reason = "handover-locked", status });
        if (!CanEdit(caller, status, Money.ToWire(handover.Origin), handover.CourierCanEdit))
        {
            throw BusinessException.Forbidden($"Ce bon a été créé par {await ReturnRows.ShortNameAsync(db, cancellationToken)} : sa composition ne vous est pas ouverte.",
                new { reason = "handover-admin-locked" });
        }
        return new Editable(handover.Id, handover.Code, handover.CourierId, handover.Origin);
    }

    /// <summary>`openHandover` : code RL-, bon et première étape (dans la transaction de l'appelant).</summary>
    public static async Task<ReturnHandover> OpenAsync(IAppDbContext db, ICurrentUser caller, string courierId, string agencyId, ReturnHandoverOrigin origin, ReturnHandoverStatus status,
        bool courierCanEdit, string? note, DateTime now, CancellationToken cancellationToken)
    {
        var code = await DocumentCodes.ReserveAsync(db, ReturnRows.HandoverPrefix, now, cancellationToken);
        var handover = new ReturnHandover
        {
            Code = code, Status = status, Origin = origin, CourierCanEdit = courierCanEdit, CourierId = courierId, AgencyId = agencyId, Note = ReturnRows.Text(note), CreatedById = caller.UserId,
            CreatedAt = now, UpdatedAt = now,
        };
        db.ReturnHandovers.Add(handover);
        db.ReturnHandoverEvents.Add(new ReturnHandoverEvent
        {
            HandoverId = handover.Id, Status = status, AuthorId = caller.UserId, CreatedAt = now,
            Comment = status == ReturnHandoverStatus.DRAFT ? "Brouillon ouvert au comptoir" : origin == ReturnHandoverOrigin.ADMIN ? "Retour demandé au livreur" : "Retour demandé par le livreur",
        });
        await db.SaveChangesAsync(cancellationToken);
        return handover;
    }

    /// <summary>`addHandoverParcels` : chaque colis contrôlé (statut, livreur, réservation) puis réservé sur ce bon.</summary>
    public static async Task<ReturnHandoverDetailDto> AddParcelsAsync(IAppDbContext db, ICurrentUser caller, string code, IReadOnlyList<string>? ids, bool? all, IReadOnlyList<string>? exclude,
        string? search, string? note, CancellationToken cancellationToken)
    {
        var handover = await LoadEditableAsync(db, caller, code, cancellationToken);
        var parcelIds = await NoteRules.ResolveSelectionAsync(ids, all, exclude,
            async _ => (await CandidatesAsync(db, caller, handover.CourierId, search, cancellationToken)).Select(l => l.Id).ToList());
        foreach (var parcelId in parcelIds)
        {
            var parcel = await db.Parcels.AsNoTracking().Where(p => p.Id == parcelId && p.DeletedAt == null).Select(p => new { p.Id, p.Code, p.StatusId, p.CourierId }).FirstOrDefaultAsync(cancellationToken)
                         ?? throw BusinessException.NotFound("Colis introuvable.");
            if (handover.Origin == ReturnHandoverOrigin.COURIER && !StatusCatalog.IsCourierReturnStatus(parcel.StatusId))
            {
                throw BusinessException.Validation($"Le colis {parcel.Code} n'est pas à retourner par le livreur (statut {parcel.StatusId}).",
                    new { field = "parcelIds", reason = "parcel-not-returnable", code = parcel.Code, statusId = parcel.StatusId });
            }
            if (!StatusCatalog.IsReturnEligible(parcel.StatusId))
            {
                throw BusinessException.Conflict($"Le colis {parcel.Code} n'est pas à rapporter.", new { reason = "parcel-not-returnable", code = parcel.Code, statusId = parcel.StatusId });
            }
            if (parcel.CourierId != handover.CourierId) throw BusinessException.Conflict($"Le colis {parcel.Code} n'est pas affecté à ce livreur.", new { reason = "parcel-other-courier", code = parcel.Code });
            var reserved = await db.ReturnHandoverItems.AsNoTracking().Where(i => i.ReservedParcelId == parcel.Id).Select(i => new { i.HandoverId, i.Handover.Code }).FirstOrDefaultAsync(cancellationToken);
            if (reserved is not null)
            {
                var same = reserved.HandoverId == handover.Id;
                throw BusinessException.Conflict(same ? $"Le colis {parcel.Code} est déjà dans ce bon." : $"Le colis {parcel.Code} figure déjà dans le bon {reserved.Code}.",
                    new { reason = same ? "parcel-already-listed" : "parcel-already-reserved", code = parcel.Code });
            }
            try
            {
                db.ReturnHandoverItems.Add(new ReturnHandoverItem { HandoverId = handover.Id, ParcelId = parcel.Id, ReservedParcelId = parcel.Id, Note = ReturnRows.Text(note), CreatedAt = DateTime.UtcNow });
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                throw BusinessException.Conflict($"Le colis {parcel.Code} est déjà dans ce bon.", new { reason = "parcel-already-listed", code = parcel.Code });
            }
        }
        return await DetailAsync(db, caller, code, cancellationToken);
    }

    /// <summary>`advanceHandover` : l'étape du bon, son journal, et celui de chaque colis (sauf demande et clôture).</summary>
    public static async Task AdvanceAsync(IAppDbContext db, ICurrentUser caller, string handoverId, ReturnHandoverStatus status, string comment, DateTime now, CancellationToken cancellationToken)
    {
        var items = status is ReturnHandoverStatus.CLOSED or ReturnHandoverStatus.RETURN_REQUESTED
            ? []
            : await db.ReturnHandoverItems.AsNoTracking().Where(i => i.HandoverId == handoverId).Select(i => new { i.ParcelId, i.Parcel.StatusId }).ToListAsync(cancellationToken);
        var code = items.Count == 0 ? null : await db.ReturnHandovers.AsNoTracking().Where(h => h.Id == handoverId).Select(h => h.Code).FirstAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var handover = await db.ReturnHandovers.FirstAsync(h => h.Id == handoverId, cancellationToken);
        handover.Status = status;
        handover.UpdatedAt = now;
        switch (status)
        {
            case ReturnHandoverStatus.PICKED_UP: handover.PickedUpAt = now; handover.PickedUpById = caller.UserId; break;
            case ReturnHandoverStatus.AWAITING_RECEIPT: handover.HandedOverAt = now; handover.HandedOverById = caller.UserId; break;
            case ReturnHandoverStatus.CLOSED: handover.ClosedAt = now; handover.ClosedById = caller.UserId; break;
        }
        db.ReturnHandoverEvents.Add(new ReturnHandoverEvent { HandoverId = handoverId, Status = status, Comment = comment, AuthorId = caller.UserId, CreatedAt = now });
        foreach (var item in items)
        {
            db.ParcelEvents.Add(new ParcelEvent { ParcelId = item.ParcelId, StatusId = item.StatusId, Comment = $"{comment} — bon {code ?? string.Empty}".Trim(), AuthorId = caller.UserId, CreatedAt = now });
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public static async Task NotifyCourierAsync(IAppDbContext db, ILogger logger, string courierId, string topic, ReturnHandoverDetailDto detail, CancellationToken cancellationToken)
    {
        try
        {
            await Notifier.NotifyCourierAsync(db, courierId, topic, new Dictionary<string, object> { ["code"] = detail.Code, ["count"] = detail.ParcelCount },
                new Dictionary<string, string> { ["topic"] = topic, ["handoverCode"] = detail.Code }, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("Notification « {Topic} » non envoyée : {Message}", topic, error.Message);
        }
    }

    /// <summary>`bulkHandoverGroups` : un groupe par livreur et par agence, colis rapportables dans l'ordre d'arrivée.</summary>
    public static async Task<List<(string OwnerId, string AgencyId, List<string> Parcels)>> BulkGroupsAsync(IAppDbContext db, ICurrentUser caller, CancellationToken cancellationToken)
    {
        var parcels = await ReturnRows.HandoverEligible(db.Parcels.AsNoTracking().Where(p => p.DeletedAt == null && p.CourierId != null))
            .OrderBy(p => p.StatusAt).ThenBy(p => p.Code).Select(p => new { p.Id, p.CourierId, p.AgencyId }).ToListAsync(cancellationToken);
        var reachable = await ReturnRows.ReachableAgenciesAsync(db, caller, cancellationToken);
        var groups = new List<(string OwnerId, string AgencyId, List<string> Parcels)>();
        foreach (var parcel in parcels)
        {
            if (reachable is not null && !reachable.Contains(parcel.AgencyId)) continue;
            var index = groups.FindIndex(g => g.OwnerId == parcel.CourierId && g.AgencyId == parcel.AgencyId);
            if (index < 0) groups.Add((parcel.CourierId!, parcel.AgencyId, [parcel.Id]));
            else groups[index].Parcels.Add(parcel.Id);
        }
        return groups;
    }
}
