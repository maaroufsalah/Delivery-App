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

/// <summary>`deleteNote`.</summary>
public static class NoteDeletion
{
    public static async Task<NoteCodeDto> DeleteAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken)
    {
        var note = await NoteRows.Scoped(db.DeliveryNotes, caller).Include(n => n.Parcels).Include(n => n.RemovedParcels).FirstOrDefaultAsync(n => n.Code == code, cancellationToken)
                   ?? throw BusinessException.NotFound($"Bon introuvable : {code}");
        if (note.Status == DeliveryNoteStatus.VALIDATED) throw BusinessException.Conflict("Ce bon est validé : il ne se supprime plus.", new { reason = "note-validated" });
        if (note.Status == DeliveryNoteStatus.PICKED_UP_BY_COURIER) throw BusinessException.Conflict("Ce bon est entre les mains du livreur : il ne se supprime plus.", new { reason = "note-picked-up" });
        if (await db.RouteNotes.AnyAsync(l => l.DeliveryNoteId == note.Id, cancellationToken)) throw BusinessException.Conflict("Ce bon est porté par un circuit : retirez-l’en d’abord.", new { reason = "note-in-route" });
        db.DeliveryNoteParcels.RemoveRange(note.Parcels);
        db.DeliveryNoteRemovedParcels.RemoveRange(note.RemovedParcels);
        db.DeliveryNotes.Remove(note);
        await db.SaveChangesAsync(cancellationToken);
        return new NoteCodeDto(code);
    }
}
