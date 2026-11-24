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

/// <summary>`blNoteSchema` : un bon BL brouillon d'un Client Pro, avec ses premiers BL.</summary>
public sealed record CreateBlNoteCommand : IRequest<BlNoteDetailDto>
{
    public string? ClientId { get; init; }
    public string? AgencyId { get; init; }
    public IReadOnlyList<string>? SlipIds { get; init; }
    public string? Note { get; init => field = Zod.Trim(value); }
}

public sealed class CreateBlNoteValidator : ZodValidator<CreateBlNoteCommand>
{
    public CreateBlNoteValidator()
    {
        RuleFor(x => x.ClientId).Required().MinLength(1, "required");
        RuleFor(x => x.AgencyId).Required().MinLength(1, "required");
        RuleFor(x => x.Note).MaxLength(500);
        RuleFor(x => x.SlipIds).Custom((ids, context) =>
        {
            if (ids is null) return;
            if (ids.Count > NoteRules.SelectionMax) context.AddFailure(ValidationPaths.Raw + "slipIds", "too-many");
            for (var i = 0; i < ids.Count; i++) if (string.IsNullOrEmpty(ids[i])) context.AddFailure(ValidationPaths.Raw + $"slipIds.{i}", Zod.TooSmallString(1));
        });
    }
}

internal sealed class CreateBlNoteHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CreateBlNoteCommand, BlNoteDetailDto>
{
    public async Task<BlNoteDetailDto> Handle(CreateBlNoteCommand input, CancellationToken cancellationToken)
    {
        var client = await db.Clients.AsNoTracking().Where(c => c.Id == input.ClientId && c.DeletedAt == null).Select(c => new { c.Id, c.IsPro }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound("E-commerçant introuvable.");
        if (!client.IsPro) throw BusinessException.Conflict("Cet e-commerçant n'est pas un Client Pro.", new { reason = "client-not-pro" });
        if (!await db.Agencies.AnyAsync(a => a.Id == input.AgencyId && a.DeletedAt == null, cancellationToken)) throw BusinessException.NotFound("Agence introuvable.");
        var slipIds = (input.SlipIds ?? []).Distinct().ToList();
        if (slipIds.Count == 0) throw BusinessException.Validation("Un bon BL sans BL ne s’ouvre pas.", new { field = "slipIds", reason = "bl-note-empty" });
        var courierId = caller.CourierId ?? await db.DeliverySlips.AsNoTracking().Where(s => s.ClientId == input.ClientId && s.Status == DeliverySlipStatus.COLLECTED && s.CourierId != null)
            .OrderBy(s => s.CollectedAt).Select(s => s.CourierId).FirstOrDefaultAsync(cancellationToken);
        if (courierId is null) throw BusinessException.Validation("Aucun livreur ne porte de BL pour cet e-commerçant.");
        var now = DateTime.UtcNow;
        string code;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            code = await DocumentCodes.ReserveAsync(db, BlRows.NotePrefix, now, cancellationToken);
            var note = new BlNote
            {
                Code = code, Status = BlNoteStatus.DRAFT, CourierId = courierId, ClientId = input.ClientId!, AgencyId = input.AgencyId!, Note = string.IsNullOrEmpty(input.Note) ? null : input.Note,
                CreatedById = caller.UserId, CreatedAt = now,
            };
            db.BlNotes.Add(note);
            db.BlNoteEvents.Add(new BlNoteEvent { NoteId = note.Id, Status = BlNoteStatus.DRAFT, Comment = "Bon BL ouvert", AuthorId = caller.UserId, CreatedAt = now });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        await BlNoteSlips.AddAsync(db, caller, code, slipIds, cancellationToken);
        return await BlRows.NoteDetailAsync(db, caller, code, cancellationToken);
    }
}
