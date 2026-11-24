using System.Globalization;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Finance;

/// <summary>`chequeBulkSchema` : une pile de chèques vers l'étape suivante ; les autres sont écartés, motivés.</summary>
public sealed record AdvanceChequesCommand : IRequest<ChequeBulkResultDto>
{
    public IReadOnlyList<string>? Ids { get; init; }
    public string? Status { get; init; }
    public string? Comment { get; init => field = Zod.Trim(value); }
}

public sealed class AdvanceChequesValidator : ZodValidator<AdvanceChequesCommand>
{
    public AdvanceChequesValidator()
    {
        RuleFor(x => x.Ids).Required("array").Custom((ids, context) =>
        {
            if (ids is null) return;
            if (ids.Count < 1) context.AddFailure(ValidationPaths.Raw + "ids", "required");
            if (ids.Count > NoteRules.SelectionMax) context.AddFailure(ValidationPaths.Raw + "ids", "too-many");
            for (var i = 0; i < ids.Count; i++) if (string.IsNullOrEmpty(ids[i])) context.AddFailure(ValidationPaths.Raw + $"ids.{i}", Zod.TooSmallString(1));
        });
        RuleFor(x => x.Status).Required().OneOf(Cheques.Statuses);
        RuleFor(x => x.Comment).MaxLength(500);
    }
}

internal sealed class AdvanceChequesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<AdvanceChequesCommand, ChequeBulkResultDto>
{
    public async Task<ChequeBulkResultDto> Handle(AdvanceChequesCommand input, CancellationToken cancellationToken)
    {
        if (caller.CourierId is not null || caller.ClientId is not null)
        {
            throw BusinessException.Forbidden("Seule l’agence fait avancer une pile de chèques.", new { reason = "cheque-is-agency-only" });
        }
        var done = new List<string>();
        var skipped = new List<ChequeBulkSkipDto>();
        foreach (var id in input.Ids!)
        {
            try
            {
                var cheque = await Cheques.LoadAsync(db, caller, id, cancellationToken);
                if (Cheques.Next(cheque.Status) != input.Status)
                {
                    skipped.Add(new ChequeBulkSkipDto(id, cheque.Number, "cheque-wrong-status", cheque.Status));
                    continue;
                }
                await Cheques.WriteAsync(db, caller, cheque.Id, input.Status!, input.Comment, DateTime.UtcNow, null, cancellationToken);
                done.Add(id);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                db.ChangeTracker.Clear();
                skipped.Add(new ChequeBulkSkipDto(id, null, "cheque-unreachable", null));
            }
        }
        return new ChequeBulkResultDto(done, skipped);
    }
}
