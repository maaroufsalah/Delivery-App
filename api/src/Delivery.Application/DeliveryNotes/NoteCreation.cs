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

/// <summary>`createNote` / `createNoteWithParcels`.</summary>
public static class NoteCreation
{
    public static async Task<string> CreateAsync(IAppDbContext db, ICurrentUser caller, DeliveryNoteType type, string clientId, string agencyId, string? locationCityId,
        IReadOnlyList<string>? parcelIds, CancellationToken cancellationToken)
    {
        if (parcelIds is { Count: 0 }) throw BusinessException.Validation("Un bon sans colis ne s’ouvre pas.", new { field = "parcelIds" });
        var client = await db.Clients.AsNoTracking().Where(c => c.Id == clientId && c.DeletedAt == null).Select(c => new { c.Id, c.CityId }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound("E-commerçant introuvable.");
        if (!await db.Agencies.AnyAsync(a => a.Id == agencyId && a.DeletedAt == null, cancellationToken)) throw BusinessException.NotFound("Agence introuvable.");
        var parcels = parcelIds is null ? null : await NoteRules.AssertParcelsAsync(db, clientId, type, parcelIds, null, cancellationToken);
        var now = DateTime.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (parcels is not null) await NoteRules.LockFreeOfNotesAsync(db, parcels.Select(p => p.Id).ToList(), null, cancellationToken);
        var code = await DocumentCodes.ReserveAsync(db, type == DeliveryNoteType.PICKUP ? NoteRules.PickupPrefix : NoteRules.DropoffPrefix, now, cancellationToken);
        var note = new DeliveryNote
        {
            Code = code,
            Type = type,
            Status = DeliveryNoteStatus.AWAITING_VALIDATION,
            ClientId = clientId,
            AgencyId = agencyId,
            LocationCityId = string.IsNullOrEmpty(locationCityId) ? client.CityId : locationCityId,
            CreatedAt = now,
        };
        db.DeliveryNotes.Add(note);
        if (parcels is not null)
        {
            foreach (var parcel in parcels) db.DeliveryNoteParcels.Add(new DeliveryNoteParcel { DeliveryNoteId = note.Id, ParcelId = parcel.Id });
            await NoteRules.AlignParcelsAsync(db, caller, parcels, type, code, note.LocationCityId, now, cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return code;
    }
}
