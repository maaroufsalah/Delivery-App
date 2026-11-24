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

/// <summary>`routeParcelsSchema` : des colis cochés, ou « tout » (recherche, ville, quartier) moins des exclus.</summary>
public sealed record AddRouteParcelsCommand : ParcelSelection, IRequest<RouteDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? District { get; init => field = Zod.Trim(value); }
    public string? CityId { get; init => field = Zod.Trim(value); }
}

public sealed class AddRouteParcelsValidator : ParcelSelectionValidator<AddRouteParcelsCommand>
{
    public AddRouteParcelsValidator()
    {
        RuleFor(x => x.District).MaxLength(120);
        RuleFor(x => x.CityId).MaxLength(64);
    }
}

internal sealed class AddRouteParcelsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<AddRouteParcelsCommand, RouteDetailDto>
{
    public async Task<RouteDetailDto> Handle(AddRouteParcelsCommand input, CancellationToken cancellationToken)
    {
        var route = await RouteRows.FindAsync(db, caller, input.Code, cancellationToken);
        RouteWrites.AssertDraft(route.Status);
        List<string> ids;
        try
        {
            ids = await NoteRules.ResolveSelectionAsync(input.ParcelIds, input.All, input.Exclude, async _ =>
                RouteWrites.InDistrict(RouteWrites.InCity(await RouteWrites.CandidatesAsync(db, caller, input.Code, input.Search, cancellationToken), input.CityId), input.District)
                    .Where(r => r.HeldBy?.Locked != true).Select(r => r.Id).ToList());
        }
        catch (BusinessException error) when (error.Details?.GetType().GetProperty("reason")?.GetValue(error.Details) as string == "selection-too-large")
        {
            throw BusinessException.Validation($"Plus de {NoteRules.SelectionMax} colis sélectionnés : filtrez par ville ou par quartier.", new { reason = "selection-too-large", limit = NoteRules.SelectionMax });
        }
        await RouteWrites.AddParcelsAsync(db, caller, route, ids, cancellationToken);
        return await RouteRows.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}
