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

/// <summary>`routeConfirmSchema` : le livreur rend compte d'un colis (en main, et son issue).</summary>
public sealed record ConfirmRouteParcelCommand : RouteConfirmBody, IRequest<RouteDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    [JsonIgnore]
    public string ParcelId { get; init; } = string.Empty;

    [JsonIgnore]
    public bool AdminFieldRevision { get; init; }
}

public sealed class ConfirmRouteParcelValidator : RouteConfirmBodyValidator<ConfirmRouteParcelCommand>;

internal sealed class ConfirmRouteParcelHandler(IAppDbContext db, ICurrentUser caller, ILogger<ConfirmRouteParcelHandler> logger) : IRequestHandler<ConfirmRouteParcelCommand, RouteDetailDto>
{
    public async Task<RouteDetailDto> Handle(ConfirmRouteParcelCommand input, CancellationToken cancellationToken)
    {
        await RouteParcelConfirm.RunAsync(db, caller, logger, input.Code, input.ParcelId, input.ToOutcome(), input.AdminFieldRevision, cancellationToken);
        return await RouteRows.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}
