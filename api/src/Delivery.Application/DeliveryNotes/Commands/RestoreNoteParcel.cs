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

/// <summary>`POST /delivery-notes/:code/removed/:parcelId/restore` : un colis retiré par erreur revient en attente de ramassage.</summary>
public sealed record RestoreNoteParcelCommand(string Code, string ParcelId) : IRequest<DeliveryNoteDetailDto>;

internal sealed class RestoreNoteParcelHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RestoreNoteParcelCommand, DeliveryNoteDetailDto>
{
    public async Task<DeliveryNoteDetailDto> Handle(RestoreNoteParcelCommand request, CancellationToken cancellationToken)
    {
        var note = await NoteRows.Scoped(db.DeliveryNotes.AsNoTracking(), caller).FirstOrDefaultAsync(n => n.Code == request.Code, cancellationToken)
                   ?? throw BusinessException.NotFound($"Bon introuvable : {request.Code}");
        var removal = await db.DeliveryNoteRemovedParcels.Include(r => r.Parcel).FirstOrDefaultAsync(r => r.DeliveryNoteId == note.Id && r.ParcelId == request.ParcelId, cancellationToken)
                      ?? throw BusinessException.NotFound("Ce colis n’a pas été retiré de ce bon.");
        if (removal.RestoredAt is not null) throw BusinessException.Conflict("Ce colis est déjà restauré.", new { reason = "already-restored" });
        var parcel = removal.Parcel;
        if (parcel.DeletedAt is null)
        {
            throw BusinessException.Conflict($"Le colis {parcel.Code} n’a pas été supprimé : il est déjà en attente de ramassage.", new { reason = "not-deleted" });
        }
        var restorable = note.Status != DeliveryNoteStatus.VALIDATED
                         && (removal.RouteId is null || await db.Routes.Where(r => r.Id == removal.RouteId).Select(r => r.Status).FirstOrDefaultAsync(cancellationToken) != RouteStatus.CLOSED);
        if (!restorable)
        {
            throw BusinessException.Validation($"{note.Code} est validé : un colis retiré ne se restaure plus. S’il est retrouvé, recréez-le ; il partira sur un nouveau bon.", new { reason = "note-restore-locked" });
        }
        var now = DateTime.UtcNow;
        var author = await ParcelStatusChange.AuthorNameAsync(db, caller.UserId, cancellationToken);
        var deletedAt = parcel.DeletedAt;
        parcel.DeletedAt = null;
        parcel.CourierId = null;
        if (parcel.StatusId != NoteRules.AwaitingPickup)
        {
            parcel.StatusId = NoteRules.AwaitingPickup;
            parcel.StatusAt = now;
            db.ParcelEvents.Add(new ParcelEvent { ParcelId = parcel.Id, StatusId = NoteRules.AwaitingPickup, Comment = NoteRules.RestoreReason(note.Code), AuthorId = caller.UserId, CityId = note.LocationCityId, CreatedAt = now });
        }
        if (parcel.PickupWaived) parcel.PickupWaived = false;
        removal.RestoredAt = now;
        removal.RestoredBy = caller.UserId;
        ParcelJournal.Record(db, caller, parcel.Id, "deletedAt", ParcelAdmin.Iso(deletedAt!.Value), string.Empty, NoteRules.RestoreReason(note.Code), author, now);
        await db.SaveChangesAsync(cancellationToken);
        return await NoteRows.DetailAsync(db, caller, request.Code, cancellationToken);
    }
}
