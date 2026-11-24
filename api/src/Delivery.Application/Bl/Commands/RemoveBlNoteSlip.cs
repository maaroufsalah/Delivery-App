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

/// <summary>`DELETE /bl/notes/:code/slips/:slipId`.</summary>
public sealed record RemoveBlNoteSlipCommand(string Code, string SlipId) : IRequest<BlNoteDetailDto>;

internal sealed class RemoveBlNoteSlipHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RemoveBlNoteSlipCommand, BlNoteDetailDto>
{
    public async Task<BlNoteDetailDto> Handle(RemoveBlNoteSlipCommand request, CancellationToken cancellationToken)
    {
        var note = await BlRows.Notes(db.BlNotes.AsNoTracking(), caller).Where(n => n.Code == request.Code).Select(n => new { n.Id, n.Status }).FirstOrDefaultAsync(cancellationToken)
                   ?? throw BusinessException.NotFound($"Bon BL introuvable : {request.Code}");
        if (note.Status != BlNoteStatus.DRAFT) throw BusinessException.Conflict("Ce bon BL ne se modifie plus.", new { reason = "bl-note-locked" });
        await db.BlNoteItems.Where(i => i.NoteId == note.Id && i.SlipId == request.SlipId).ExecuteDeleteAsync(cancellationToken);
        return await BlRows.NoteDetailAsync(db, caller, request.Code, cancellationToken);
    }
}
