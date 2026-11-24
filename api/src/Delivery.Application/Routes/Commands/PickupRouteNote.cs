using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Routes;

/// <summary>`routeNotePickupSchema` : les colis pris chez le vendeur ; `removeUnpicked` retire les autres du bon.</summary>
public sealed record PickupRouteNoteCommand : IRequest<RouteDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    [JsonIgnore]
    public string NoteId { get; init; } = string.Empty;

    public IReadOnlyList<string>? ParcelIds { get; init; }
    public bool? RemoveUnpicked { get; init; }
}

public sealed class PickupRouteNoteValidator : ZodValidator<PickupRouteNoteCommand>
{
    public PickupRouteNoteValidator()
    {
        RuleFor(x => x.ParcelIds).Required("array").Custom((ids, context) =>
        {
            if (ids is null) return;
            if (ids.Count < 1) context.AddFailure(ValidationPaths.Raw + "parcelIds", "required");
            if (ids.Count > NoteRules.SelectionMax) context.AddFailure(ValidationPaths.Raw + "parcelIds", "too-many");
            for (var i = 0; i < ids.Count; i++) if (string.IsNullOrEmpty(ids[i])) context.AddFailure(ValidationPaths.Raw + $"parcelIds.{i}", Zod.TooSmallString(1));
        });
    }
}

internal sealed class PickupRouteNoteHandler(IAppDbContext db, ICurrentUser caller, ILogger<PickupRouteNoteHandler> logger) : IRequestHandler<PickupRouteNoteCommand, RouteDetailDto>
{
    public async Task<RouteDetailDto> Handle(PickupRouteNoteCommand input, CancellationToken cancellationToken)
    {
        var route = await RouteRows.FindAsync(db, caller, input.Code, cancellationToken);
        await RouteWrites.PickupNoteAsync(db, caller, logger, route, input.NoteId, input.ParcelIds!, input.RemoveUnpicked == true, cancellationToken);
        return await RouteRows.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}
