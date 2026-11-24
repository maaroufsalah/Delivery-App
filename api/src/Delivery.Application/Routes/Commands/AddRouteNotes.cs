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

/// <summary>`routeNotesSchema` : des bons cochés, ou « tout » moins des exclus.</summary>
public sealed record AddRouteNotesCommand : IRequest<RouteDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public IReadOnlyList<string>? NoteIds { get; init; }
    public bool? All { get; init; }
    public string? Search { get; init => field = Zod.Trim(value); }
    public IReadOnlyList<string>? Exclude { get; init; }
}

public sealed class AddRouteNotesValidator : ZodValidator<AddRouteNotesCommand>
{
    public AddRouteNotesValidator()
    {
        RuleFor(x => x.Search).MaxLength(120);
    }

    protected override void Refine(AddRouteNotesCommand value, Action<string, string> issue)
    {
        if (value.All != true && (value.NoteIds?.Count ?? 0) == 0) issue(ValidationPaths.Raw + "noteIds", "required");
    }
}

internal sealed class AddRouteNotesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<AddRouteNotesCommand, RouteDetailDto>
{
    public async Task<RouteDetailDto> Handle(AddRouteNotesCommand input, CancellationToken cancellationToken)
    {
        var route = await RouteRows.FindAsync(db, caller, input.Code, cancellationToken);
        RouteWrites.AssertDraft(route.Status);
        if (route.Kind != RouteKind.PICKUP) throw BusinessException.Conflict("Seul un circuit de ramassage porte des bons.", new { reason = "route-not-pickup" });
        var ids = await NoteRules.ResolveSelectionAsync(input.NoteIds, input.All, input.Exclude,
            async _ => (await RouteWrites.NoteCandidatesAsync(db, caller, input.Code, input.Search, cancellationToken)).Select(n => n.Id).ToList());
        await RouteWrites.AddNotesAsync(db, route, ids, cancellationToken);
        return await RouteRows.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}
