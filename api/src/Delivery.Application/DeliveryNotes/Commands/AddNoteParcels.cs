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

/// <summary>`POST /delivery-notes/:code/parcels` (`deliveryNoteParcelsSchema`).</summary>
public sealed record AddNoteParcelsCommand : ParcelSelection, IRequest<DeliveryNoteDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;
}

public sealed class AddNoteParcelsValidator : ParcelSelectionValidator<AddNoteParcelsCommand>;

internal sealed class AddNoteParcelsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<AddNoteParcelsCommand, DeliveryNoteDetailDto>
{
    public async Task<DeliveryNoteDetailDto> Handle(AddNoteParcelsCommand input, CancellationToken cancellationToken)
    {
        var note = await NoteRows.Scoped(db.DeliveryNotes.AsNoTracking(), caller).FirstOrDefaultAsync(n => n.Code == input.Code, cancellationToken)
                   ?? throw BusinessException.NotFound($"Bon introuvable : {input.Code}");
        if (note.Status == DeliveryNoteStatus.VALIDATED) throw BusinessException.Conflict("Ce bon est validé : sa composition ne change plus.", new { reason = "note-validated" });
        var ids = await NoteRules.ResolveSelectionAsync(input.ParcelIds, input.All, input.Exclude, take => NoteRules.CandidateIdsAsync(db, note.ClientId, input.Search, take, cancellationToken));
        var parcels = await NoteRules.AssertParcelsAsync(db, note.ClientId, note.Type, ids, note.Id, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await NoteRules.LockFreeOfNotesAsync(db, parcels.Select(p => p.Id).ToList(), note.Id, cancellationToken);
        var existing = (await db.DeliveryNoteParcels.Where(l => l.DeliveryNoteId == note.Id).Select(l => l.ParcelId).ToListAsync(cancellationToken)).ToHashSet();
        foreach (var parcel in parcels.Where(p => !existing.Contains(p.Id))) db.DeliveryNoteParcels.Add(new DeliveryNoteParcel { DeliveryNoteId = note.Id, ParcelId = parcel.Id });
        await NoteRules.AlignParcelsAsync(db, caller, parcels, note.Type, note.Code, note.LocationCityId, DateTime.UtcNow, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await NoteRows.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}
