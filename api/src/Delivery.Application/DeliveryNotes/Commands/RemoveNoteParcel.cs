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

/// <summary>`DELETE /delivery-notes/:code/parcels/:parcelId`.</summary>
public sealed record RemoveNoteParcelCommand(string Code, string ParcelId) : IRequest<DeliveryNoteDetailDto>;

internal sealed class RemoveNoteParcelHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RemoveNoteParcelCommand, DeliveryNoteDetailDto>
{
    public async Task<DeliveryNoteDetailDto> Handle(RemoveNoteParcelCommand request, CancellationToken cancellationToken)
    {
        var note = await NoteRows.Scoped(db.DeliveryNotes.AsNoTracking(), caller).Where(n => n.Code == request.Code).Select(n => new { n.Id, n.Status }).FirstOrDefaultAsync(cancellationToken)
                   ?? throw BusinessException.NotFound($"Bon introuvable : {request.Code}");
        if (note.Status == DeliveryNoteStatus.VALIDATED) throw BusinessException.Conflict("Ce bon est validé : sa composition ne change plus.");
        db.DeliveryNoteParcels.RemoveRange(await db.DeliveryNoteParcels.Where(l => l.DeliveryNoteId == note.Id && l.ParcelId == request.ParcelId).ToListAsync(cancellationToken));
        await db.SaveChangesAsync(cancellationToken);
        return await NoteRows.DetailAsync(db, caller, request.Code, cancellationToken);
    }
}
