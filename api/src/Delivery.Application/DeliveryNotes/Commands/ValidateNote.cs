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

/// <summary>`deliveryNoteValidateSchema` : sans liste, tout le bon ; avec, seuls ceux-là (les autres sont retirés).</summary>
public sealed record ValidateNoteCommand : IRequest<object>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    [JsonIgnore]
    public bool Async { get; init; }

    public IReadOnlyList<string>? ParcelIds { get; init; }
}

public sealed class ValidateNoteValidator : ZodValidator<ValidateNoteCommand>
{
    public ValidateNoteValidator()
    {
        RuleFor(x => x.ParcelIds).Custom((ids, context) =>
        {
            if (ids is null) return;
            if (ids.Count < 1) context.AddFailure(ValidationPaths.Raw + "parcelIds", "required");
            if (ids.Count > NoteRules.SelectionMax) context.AddFailure(ValidationPaths.Raw + "parcelIds", "too-many");
            for (var i = 0; i < ids.Count; i++) if (string.IsNullOrEmpty(ids[i])) context.AddFailure(ValidationPaths.Raw + $"parcelIds.{i}", Zod.TooSmallString(1));
        });
    }
}

internal sealed class ValidateNoteHandler(IAppDbContext db, ICurrentUser caller, ILogger<ValidateNoteHandler> logger, BulkJobs.BulkJobQueue jobs) : IRequestHandler<ValidateNoteCommand, object>
{
    public async Task<object> Handle(ValidateNoteCommand input, CancellationToken cancellationToken)
    {
        var note = await NoteRows.Scoped(db.DeliveryNotes.AsNoTracking(), caller).Where(n => n.Code == input.Code)
            .Select(n => new { n.Id, n.Code, n.Status, Parcels = n.Parcels.Select(l => new { l.ParcelId, l.Parcel.StatusId }).ToList() }).FirstOrDefaultAsync(cancellationToken)
            ?? throw BusinessException.NotFound($"Bon introuvable : {input.Code}");
        if (input.Async && note.Status != DeliveryNoteStatus.VALIDATED && note.Parcels.Count > NoteRules.BulkAsyncThreshold)
        {
            var kept = input.ParcelIds?.ToHashSet();
            var picked = note.Parcels.Count(p => (kept is null || kept.Contains(p.ParcelId)) && (p.StatusId == NoteRules.PickedUp || NoteRules.PickupSourceStatusIds.Contains(p.StatusId)));
            var job = await BulkJobs.BulkJobWrites.CreateAsync(db, caller, jobs, BulkJobs.BulkJobKinds.NoteValidate, note.Code,
                new Dictionary<string, object?> { ["kept"] = input.ParcelIds, ["pickedUpCount"] = picked }, note.Parcels.Select(p => p.ParcelId).ToList(), cancellationToken);
            return new BulkJobs.BulkJobAcceptedDto(job);
        }
        await BulkJobs.BulkJobWrites.AssertIdleAsync(db, note.Code, cancellationToken);
        await NoteValidation.RunAsync(db, caller, logger, input.Code, input.ParcelIds, new NoteValidation.Options(), cancellationToken);
        return await NoteRows.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}
