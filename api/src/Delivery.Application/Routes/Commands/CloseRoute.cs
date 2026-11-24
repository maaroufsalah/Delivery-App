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

/// <summary>`routeCloseSchema` : clôture ; un ramassage exige `removeUnpicked` quand des colis n'ont pas été pris.</summary>
public sealed record CloseRouteCommand : IRequest<RouteDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public bool? RemoveUnpicked { get; init; }
}

public sealed class CloseRouteValidator : ZodValidator<CloseRouteCommand>;

internal sealed class CloseRouteHandler(IAppDbContext db, ICurrentUser caller, ILogger<CloseRouteHandler> logger) : IRequestHandler<CloseRouteCommand, RouteDetailDto>
{
    public async Task<RouteDetailDto> Handle(CloseRouteCommand input, CancellationToken cancellationToken)
    {
        var route = await RouteRows.FindAsync(db, caller, input.Code, cancellationToken);
        await RouteClosing.CloseAsync(db, caller, logger, route, input.RemoveUnpicked == true, null, cancellationToken);
        return await RouteRows.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}
