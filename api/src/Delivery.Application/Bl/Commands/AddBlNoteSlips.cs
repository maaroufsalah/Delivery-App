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

/// <summary>`blNoteItemsSchema`.</summary>
public sealed record AddBlNoteSlipsCommand : IRequest<BlNoteDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public IReadOnlyList<string>? SlipIds { get; init; }
}

public sealed class AddBlNoteSlipsValidator : ZodValidator<AddBlNoteSlipsCommand>
{
    public AddBlNoteSlipsValidator()
    {
        RuleFor(x => x.SlipIds).Required("array").Custom((ids, context) =>
        {
            if (ids is null) return;
            if (ids.Count < 1) context.AddFailure(ValidationPaths.Raw + "slipIds", "required");
            if (ids.Count > NoteRules.SelectionMax) context.AddFailure(ValidationPaths.Raw + "slipIds", "too-many");
            for (var i = 0; i < ids.Count; i++) if (string.IsNullOrEmpty(ids[i])) context.AddFailure(ValidationPaths.Raw + $"slipIds.{i}", Zod.TooSmallString(1));
        });
    }
}

internal sealed class AddBlNoteSlipsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<AddBlNoteSlipsCommand, BlNoteDetailDto>
{
    public async Task<BlNoteDetailDto> Handle(AddBlNoteSlipsCommand input, CancellationToken cancellationToken)
    {
        await BlNoteSlips.AddAsync(db, caller, input.Code, input.SlipIds!, cancellationToken);
        return await BlRows.NoteDetailAsync(db, caller, input.Code, cancellationToken);
    }
}
