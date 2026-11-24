using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Idempotency;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.CourierApp;

/// <summary>`POST /courier/parcels/:code/status` (`routeConfirmSchema`) : le livreur rapporte une issue, rejouable par `Idempotency-Key`.</summary>
public sealed record ReportCourierOutcomeCommand : RouteConfirmBody, IRequest<object?>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    [JsonIgnore]
    public string? IdempotencyKey { get; init; }
}

internal sealed class ReportCourierOutcomeHandler(IAppDbContext db, ICurrentUser caller, ILogger<ReportCourierOutcomeHandler> logger) : IRequestHandler<ReportCourierOutcomeCommand, object?>
{
    public Task<object?> Handle(ReportCourierOutcomeCommand request, CancellationToken cancellationToken) =>
        IdempotentReplay.RunAsync(db, caller, request.IdempotencyKey, $"POST /courier/parcels/{request.Code}/status", () => ReportAsync(request, cancellationToken), cancellationToken);

    private Task<CourierRouteDto> ReportAsync(ReportCourierOutcomeCommand request, CancellationToken cancellationToken) =>
        CourierParcels.ReportAsync(db, caller, logger, request.Code, request.ToOutcome() with { Confirmed = true }, cancellationToken);
}

public sealed class ReportCourierOutcomeValidator : RouteConfirmBodyValidator<ReportCourierOutcomeCommand>;
