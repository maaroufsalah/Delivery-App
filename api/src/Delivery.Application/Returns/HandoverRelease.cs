using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Parcels;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Returns;

/// <summary>`handover-release.ts` : un colis réservé sur un bon de retour et l'issue que le livreur pose sur la route.</summary>
public static class HandoverRelease
{
    public const string ReleaseReason = "Issue posée par le livreur sur son circuit de livraison";

    public static readonly ReturnHandoverStatus[] PendingStatuses = [ReturnHandoverStatus.DRAFT, ReturnHandoverStatus.RETURN_REQUESTED, ReturnHandoverStatus.PICKED_UP];

    /// <summary>Le bon déjà remis à l'agence qui réserve ce colis (son statut ne change plus).</summary>
    public static Task<string?> HandedOverOfAsync(IAppDbContext db, string parcelId, CancellationToken cancellationToken) =>
        db.ReturnHandoverItems.AsNoTracking().Where(i => i.ReservedParcelId == parcelId && i.Handover.Status == ReturnHandoverStatus.AWAITING_RECEIPT)
            .Select(i => i.Handover.Code).FirstOrDefaultAsync(cancellationToken);

    public static async Task<bool> HasPendingAsync(IAppDbContext db, string parcelId, CancellationToken cancellationToken) =>
        await db.ReturnHandoverItems.AsNoTracking().AnyAsync(i => i.ReservedParcelId == parcelId && PendingStatuses.Contains(i.Handover.Status), cancellationToken);

    /// <summary>`releaseFromPendingHandover` : le colis quitte le bon en cours, avec sa trace dans le bon et dans le journal du colis.</summary>
    public static async Task<string?> ReleaseAsync(IAppDbContext db, ICurrentUser caller, string parcelId, string parcelCode, int targetStatusId, string authorName, DateTime at,
        CancellationToken cancellationToken)
    {
        var handover = await db.ReturnHandoverItems.AsNoTracking().Where(i => i.ReservedParcelId == parcelId && PendingStatuses.Contains(i.Handover.Status))
            .Select(i => new { i.Handover.Id, i.Handover.Code, i.Handover.Status }).FirstOrDefaultAsync(cancellationToken);
        if (handover is null) return null;
        var removed = await db.ReturnHandoverItems.Where(i => i.HandoverId == handover.Id && i.ParcelId == parcelId).ExecuteDeleteAsync(cancellationToken);
        if (removed == 0) return null;
        var reason = $"{ReleaseReason} : {DocumentLabels.ParcelStatus(targetStatusId)}";
        db.ReturnHandoverEvents.Add(new ReturnHandoverEvent { HandoverId = handover.Id, Status = handover.Status, Comment = $"{parcelCode} retiré du bon : {reason}", AuthorId = caller.UserId, CreatedAt = at });
        ParcelJournal.Record(db, caller, parcelId, ParcelJournal.NoteDepartureField, handover.Code, string.Empty, reason, authorName, at);
        await db.SaveChangesAsync(cancellationToken);
        return handover.Code;
    }
}
