using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Bl;

internal static class BlNoteSlips
{
    public static async Task AddAsync(IAppDbContext db, ICurrentUser caller, string code, IReadOnlyList<string> slipIds, CancellationToken cancellationToken)
    {
        var note = await BlRows.Notes(db.BlNotes.AsNoTracking(), caller).Where(n => n.Code == code).Select(n => new { n.Id, n.Status, n.ClientId, n.CourierId }).FirstOrDefaultAsync(cancellationToken)
                   ?? throw BusinessException.NotFound($"Bon BL introuvable : {code}");
        if (note.Status != BlNoteStatus.DRAFT) throw BusinessException.Conflict("Ce bon BL ne se modifie plus.", new { reason = "bl-note-locked" });
        foreach (var slipId in slipIds.Distinct())
        {
            var slip = await db.DeliverySlips.AsNoTracking().Where(s => s.Id == slipId).Select(s => new { s.Id, s.ClientId, s.CourierId, s.Status, s.Parcel.Code }).FirstOrDefaultAsync(cancellationToken)
                       ?? throw BusinessException.NotFound("BL introuvable.");
            if (slip.Status != DeliverySlipStatus.COLLECTED) throw BusinessException.Conflict($"Le BL du colis {slip.Code} a déjà quitté la sacoche.", new { reason = "bl-not-collected" });
            if (slip.ClientId != note.ClientId) throw BusinessException.Conflict($"Le BL du colis {slip.Code} appartient à un autre e-commerçant.", new { reason = "bl-other-client" });
            if (slip.CourierId != note.CourierId) throw BusinessException.Conflict($"Le BL du colis {slip.Code} a été récupéré par un autre livreur.", new { reason = "bl-other-courier" });
            try
            {
                db.BlNoteItems.Add(new BlNoteItem { NoteId = note.Id, SlipId = slip.Id, ReservedSlipId = slip.Id });
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                throw BusinessException.Conflict($"Le BL du colis {slip.Code} est déjà sur un bon BL.", new { reason = "bl-already-reserved" });
            }
        }
    }
}
