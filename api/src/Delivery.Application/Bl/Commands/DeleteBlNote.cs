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

/// <summary>`DELETE /bl/notes/:code` : un brouillon seulement.</summary>
public sealed record DeleteBlNoteCommand(string Code) : IRequest<BlNoteCodeDto>;

internal sealed class DeleteBlNoteHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<DeleteBlNoteCommand, BlNoteCodeDto>
{
    public async Task<BlNoteCodeDto> Handle(DeleteBlNoteCommand request, CancellationToken cancellationToken)
    {
        var note = await BlRows.Notes(db.BlNotes, caller).FirstOrDefaultAsync(n => n.Code == request.Code, cancellationToken) ?? throw BusinessException.NotFound($"Bon BL introuvable : {request.Code}");
        if (note.Status != BlNoteStatus.DRAFT) throw BusinessException.Conflict("Ce bon BL a quitté la sacoche : il ne se supprime plus.", new { reason = "bl-note-locked" });
        db.BlNotes.Remove(note);
        await db.SaveChangesAsync(cancellationToken);
        return new BlNoteCodeDto(request.Code);
    }
}
