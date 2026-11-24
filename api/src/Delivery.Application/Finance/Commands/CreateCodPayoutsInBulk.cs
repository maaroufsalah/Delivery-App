using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Application.PettyCash;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Finance;

/// <summary>`codPayoutBulkSchema` : des vendeurs cochés, ou « tous » ; un verrou en base par vendeur.</summary>
public sealed record CreateCodPayoutsInBulkCommand : IRequest<CodPayoutBulkResultDto>
{
    public string? From { get; init => field = Zod.Trim(value); }
    public string? To { get; init => field = Zod.Trim(value); }
    public string? AgencyId { get; init => field = Zod.Trim(value); }
    public bool? All { get; init; }
    public IReadOnlyList<string>? ClientIds { get; init; }
    public string? Note { get; init => field = Zod.Trim(value); }
}

public sealed class CreateCodPayoutsInBulkValidator : ZodValidator<CreateCodPayoutsInBulkCommand>
{
    public CreateCodPayoutsInBulkValidator()
    {
        RuleFor(x => x.ClientIds).Custom((ids, context) =>
        {
            if (ids is null) return;
            if (ids.Count > NoteRules.SelectionMax) context.AddFailure(ValidationPaths.Raw + "clientIds", "too-many");
            for (var i = 0; i < ids.Count; i++) if (string.IsNullOrEmpty(ids[i])) context.AddFailure(ValidationPaths.Raw + $"clientIds.{i}", Zod.TooSmallString(1));
        });
        RuleFor(x => x.Note).MaxLength(500);
    }

    protected override void Refine(CreateCodPayoutsInBulkCommand value, Action<string, string> issue)
    {
        if (value.All != true && (value.ClientIds?.Count ?? 0) == 0) issue(ValidationPaths.Raw + "clientIds", "required");
    }
}

internal sealed class CreateCodPayoutsInBulkHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CreateCodPayoutsInBulkCommand, CodPayoutBulkResultDto>
{
    public async Task<CodPayoutBulkResultDto> Handle(CreateCodPayoutsInBulkCommand input, CancellationToken cancellationToken)
    {
        var clientIds = input.All == true
            ? (await CodPayouts.BulkCandidatesAsync(db, caller, input.From, input.To, input.AgencyId, cancellationToken)).Select(r => r.ClientId).Distinct().ToList()
            : (input.ClientIds ?? []).Distinct().ToList();
        return await CodPayouts.CreateBulkAsync(db, caller, clientIds, input.From, input.To, input.AgencyId, input.Note, cancellationToken);
    }
}
