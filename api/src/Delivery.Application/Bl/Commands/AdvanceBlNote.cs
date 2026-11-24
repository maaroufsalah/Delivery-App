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

/// <summary>Les quatre étapes d'un bon BL (`advanceNote`) : remis, reçu, rendu au vendeur, clôturé.</summary>
public sealed record AdvanceBlNoteCommand : IRequest<BlNoteDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    [JsonIgnore]
    public string Step { get; init; } = string.Empty;

    public string? ReturnedToName { get; init => field = Zod.Trim(value); }
}

public sealed class AdvanceBlNoteValidator : ZodValidator<AdvanceBlNoteCommand>
{
    public AdvanceBlNoteValidator()
    {
        RuleFor(x => x.ReturnedToName).Required().MinLength(2, "too-short").MaxLength(120).When(x => x.Step == "return");
    }
}

internal sealed class AdvanceBlNoteHandler(IAppDbContext db, ICurrentUser caller, ILogger<AdvanceBlNoteHandler> logger) : IRequestHandler<AdvanceBlNoteCommand, BlNoteDetailDto>
{
    private sealed record Step(BlNoteStatus From, BlNoteStatus To, DeliverySlipStatus SlipStatus, string Comment, string Refusal);

    public async Task<BlNoteDetailDto> Handle(AdvanceBlNoteCommand input, CancellationToken cancellationToken)
    {
        var shortName = await BlRows.ShortNameAsync(db, cancellationToken);
        var step = input.Step switch
        {
            "handover" => new Step(BlNoteStatus.DRAFT, BlNoteStatus.AWAITING_RECEIPT, DeliverySlipStatus.HANDED_OVER, "Liasse remise à l’entrepôt", "Ce bon BL a déjà quitté la sacoche."),
            "receive" => new Step(BlNoteStatus.AWAITING_RECEIPT, BlNoteStatus.RECEIVED, DeliverySlipStatus.RECEIVED, $"Réception confirmée par {shortName}",
                "Ce bon BL doit être remis à l’entrepôt avant d’être reçu."),
            "return" => new Step(BlNoteStatus.RECEIVED, BlNoteStatus.RETURNED, DeliverySlipStatus.RETURNED, $"Liasse remise à {input.ReturnedToName}",
                "La réception de ce bon BL n’est pas confirmée."),
            _ => new Step(BlNoteStatus.RETURNED, BlNoteStatus.CLOSED, DeliverySlipStatus.CLOSED, "Bon BL clôturé", "Seul un bon BL remis au vendeur se clôture."),
        };
        var note = await BlRows.Notes(db.BlNotes, caller).FirstOrDefaultAsync(n => n.Code == input.Code, cancellationToken) ?? throw BusinessException.NotFound($"Bon BL introuvable : {input.Code}");
        if (note.Status != step.From) throw BusinessException.Conflict(step.Refusal, new { reason = "bl-note-wrong-status" });
        var slipIds = await db.BlNoteItems.Where(i => i.NoteId == note.Id).Select(i => i.SlipId).ToListAsync(cancellationToken);
        if (slipIds.Count == 0) throw BusinessException.Validation("Un bon BL sans BL ne se valide pas.");
        var now = DateTime.UtcNow;
        List<BlRows.Moved> moved;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            note.Status = step.To;
            switch (step.To)
            {
                case BlNoteStatus.AWAITING_RECEIPT: note.HandedOverAt = now; note.HandedOverById = caller.UserId; break;
                case BlNoteStatus.RECEIVED: note.ReceivedAt = now; note.ReceivedById = caller.UserId; break;
                case BlNoteStatus.RETURNED: note.ReturnedAt = now; note.ReturnedById = caller.UserId; note.ReturnedToName = input.ReturnedToName; break;
                default: note.ClosedAt = now; note.ClosedById = caller.UserId; break;
            }
            db.BlNoteEvents.Add(new BlNoteEvent { NoteId = note.Id, Status = step.To, Comment = step.Comment, AuthorId = caller.UserId, CreatedAt = now });
            await db.SaveChangesAsync(cancellationToken);
            moved = await BlRows.AdvanceSlipsAsync(db, caller, db.DeliverySlips.Where(s => slipIds.Contains(s.Id)), step.SlipStatus, step.Comment, $"{step.Comment} — bon BL {note.Code}", now,
                input.ReturnedToName, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        db.ChangeTracker.Clear();
        var detail = await BlRows.NoteDetailAsync(db, caller, input.Code, cancellationToken);
        if (step.To == BlNoteStatus.AWAITING_RECEIPT)
        {
            try
            {
                await Notifications.Notifier.NotifyStaffAsync(db, detail.AgencyId, "bl-note-awaiting", "returns.confirm",
                    new Dictionary<string, object> { ["code"] = detail.Code, ["count"] = detail.SlipCount },
                    new Dictionary<string, string> { ["topic"] = "bl-note-awaiting", ["blNoteCode"] = detail.Code }, cancellationToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogWarning("Notification « bl-note-awaiting » non envoyée : {Message}", error.Message);
            }
        }
        await BlRows.NotifySlipStepAsync(db, logger, moved, step.SlipStatus, detail.Code, shortName, cancellationToken);
        return detail;
    }
}
