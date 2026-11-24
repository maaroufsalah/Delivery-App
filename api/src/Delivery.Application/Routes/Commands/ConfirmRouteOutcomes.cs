using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.BulkJobs;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Routes;

/// <summary>`routeBulkConfirmSchema` : la même issue pour les colis cochés, en une transaction (D-146).</summary>
public sealed record ConfirmRouteOutcomesCommand : IRequest<object>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    [JsonIgnore]
    public bool Async { get; init; }

    [JsonIgnore]
    public bool FromJob { get; init; }

    public IReadOnlyList<string>? ParcelIds { get; init; }
    public JsonElement? StatusId { get; init; }
    public string? ScheduledAt { get; init => field = Zod.Trim(value); }
    public string? Comment { get; init => field = Zod.Trim(value); }
}

public sealed class ConfirmRouteOutcomesValidator : ZodValidator<ConfirmRouteOutcomesCommand>
{
    public ConfirmRouteOutcomesValidator()
    {
        RuleFor(x => x.ParcelIds).Required("array").Custom((ids, context) =>
        {
            if (ids is null) return;
            if (ids.Count < 1) context.AddFailure(ValidationPaths.Raw + "parcelIds", "required");
            if (ids.Count > NoteRules.SelectionMax) context.AddFailure(ValidationPaths.Raw + "parcelIds", "too-many");
            for (var i = 0; i < ids.Count; i++) if (string.IsNullOrEmpty(ids[i])) context.AddFailure(ValidationPaths.Raw + $"parcelIds.{i}", Zod.TooSmallString(1));
        });
        RuleFor(x => x.Comment).MaxLength(255);
    }

    protected override void Refine(ConfirmRouteOutcomesCommand value, Action<string, string> issue)
    {
        if (value.StatusId is null) issue(ValidationPaths.Raw + "statusId", Zod.Expected("number"));
        else RouteInputs.PositiveInt(value.StatusId, "statusId", issue);
    }
}

internal sealed class ConfirmRouteOutcomesHandler(IAppDbContext db, ICurrentUser caller, ILogger<ConfirmRouteOutcomesHandler> logger, BulkJobQueue jobs) : IRequestHandler<ConfirmRouteOutcomesCommand, object>
{
    public Task<object> Handle(ConfirmRouteOutcomesCommand input, CancellationToken cancellationToken) => RouteBulkOutcomes.RunAsync(db, caller, logger, jobs, input, cancellationToken);
}
