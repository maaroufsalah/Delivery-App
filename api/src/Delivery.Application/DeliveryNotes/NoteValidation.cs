using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.DeliveryNotes;

/// <summary>`validateNote` : en une fois, ou par morceaux depuis une tâche de masse.</summary>
public static class NoteValidation
{
    public sealed record Options(bool ParcelsPhase = false, bool FinishPhase = false, IReadOnlyList<string>? ChunkIds = null, int? PickedUpCount = null, int? RemovedTotal = null);

    public sealed record ChunkResult(int Removed, int Validated);

    public static async Task<ChunkResult> RunAsync(IAppDbContext db, ICurrentUser caller, ILogger logger, string code, IReadOnlyList<string>? keptIds, Options options, CancellationToken cancellationToken)
    {
        var note = await NoteRows.Scoped(db.DeliveryNotes, caller).FirstOrDefaultAsync(n => n.Code == code, cancellationToken)
                   ?? throw BusinessException.NotFound($"Bon introuvable : {code}");
        var links = await db.DeliveryNoteParcels.Where(l => l.DeliveryNoteId == note.Id).Include(l => l.Parcel).ToListAsync(cancellationToken);
        if (links.Count == 0) throw BusinessException.Validation("Un bon sans colis ne se valide pas.");
        var kept = keptIds?.ToHashSet();
        if (kept is not null && kept.Any(id => links.All(l => l.ParcelId != id)))
        {
            throw BusinessException.Validation($"Un colis coché n'appartient pas au bon {note.Code}.", new { reason = "parcel-not-in-note", noteCode = note.Code });
        }
        var chunk = options.ParcelsPhase ? (options.ChunkIds ?? []).ToHashSet() : null;
        bool InChunk(DeliveryNoteParcel l) => chunk is null || chunk.Contains(l.ParcelId);
        var removed = kept is null ? [] : links.Where(l => !kept.Contains(l.ParcelId) && InChunk(l)).ToList();
        var openRouteIds = await NoteRules.OpenRoutesOfNoteAsync(db, note.Id, cancellationToken);
        if (note.Status == DeliveryNoteStatus.VALIDATED && (removed.Count == 0 || openRouteIds.Count == 0)) throw BusinessException.Conflict("Ce bon est déjà validé.");
        var now = DateTime.UtcNow;
        var policy = await NoteRules.PolicyAsync(db, note.AgencyId, cancellationToken);
        var author = removed.Count > 0 ? await ParcelStatusChange.AuthorNameAsync(db, caller.UserId, cancellationToken) : string.Empty;
        var validated = links.Where(l => (kept is null || kept.Contains(l.ParcelId)) && InChunk(l)).ToList();
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            if (removed.Count > 0)
            {
                await NoteRules.RemoveFromNoteAsync(db, caller, note, removed.Select(l => l.Parcel).ToList(), openRouteIds, author, note.LocationCityId, now, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }
            if (note.Status != DeliveryNoteStatus.VALIDATED)
            {
                await NoteRules.ApplyValidationAsync(db, caller, note, validated.Select(l => l.Parcel).ToList(), policy, now, cancellationToken,
                    parcelsOnly: options.ParcelsPhase, noteOnly: options.FinishPhase, pickedUpCount: options.PickedUpCount);
            }
            if (openRouteIds.Count > 0 && !options.ParcelsPhase)
            {
                var ids = validated.Select(l => l.ParcelId).ToList();
                foreach (var link in await db.RouteParcels.Where(l => openRouteIds.Contains(l.RouteId) && ids.Contains(l.ParcelId) && !l.Confirmed).ToListAsync(cancellationToken))
                {
                    link.Confirmed = true;
                    link.ConfirmedAt = now;
                    link.ConfirmedBy = caller.UserId;
                }
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        if (!options.ParcelsPhase)
        {
            var announced = options.FinishPhase ? options.RemovedTotal ?? 0 : removed.Count;
            if (announced > 0) await AnnounceRemovalAsync(db, logger, note, announced, cancellationToken);
        }
        return new ChunkResult(removed.Count, validated.Count);
    }

    /// <summary>`announceRemoval` : le vendeur apprend combien de colis n'ont pas été remis.</summary>
    private static async Task AnnounceRemovalAsync(IAppDbContext db, ILogger logger, DeliveryNote note, int removed, CancellationToken cancellationToken)
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
