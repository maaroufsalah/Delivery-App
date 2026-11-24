using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Idempotency;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.CourierApp;
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

namespace Delivery.Application.Providers;

/// <summary>`providerStatusSchema` : un statut de prestataire, horodaté, sur un colis de son livreur.</summary>
public sealed record ProviderStatusCommand : IRequest<ProviderStatusResultDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    [JsonIgnore]
    public string? EventKey { get; init; }

    [JsonIgnore]
    public ProviderContext? Provider { get; init; }

    public JsonElement? ProviderStatus { get; init; }
    public string? OccurredAt { get; init; }
    public string? Comment { get; init => field = Zod.Trim(value); }
    public string? ScheduledAt { get; init => field = Zod.Trim(value); }
    public JsonElement? CollectedAmount { get; init; }
    public string? ProviderCode { get; init => field = Zod.Trim(value); }
}

public sealed class ProviderStatusValidator : ZodValidator<ProviderStatusCommand>
{
    public ProviderStatusValidator()
    {
        RuleFor(x => x.OccurredAt).Required().Must(v => v is null || ProviderStatusReport.IsIsoWithOffset(v)).WithMessage("Invalid ISO datetime");
        RuleFor(x => x.Comment).MaxLength(255);
        RuleFor(x => x.ProviderCode).MaxLength(40);
    }

    protected override void Refine(ProviderStatusCommand value, Action<string, string> issue)
    {
        if (value.ProviderStatus is null) issue(ValidationPaths.Raw + "providerStatus", Zod.Expected("number"));
        else if (value.ProviderStatus.Value.ValueKind != JsonValueKind.Number) issue(ValidationPaths.Raw + "providerStatus", Zod.Expected("number", value.ProviderStatus.Value.ValueKind == JsonValueKind.String ? "string" : "object"));
        else if (Math.Floor(value.ProviderStatus.Value.GetDouble()) != value.ProviderStatus.Value.GetDouble()) issue(ValidationPaths.Raw + "providerStatus", Zod.ExpectedInt);
        else if (value.ProviderStatus.Value.GetDouble() <= 0) issue(ValidationPaths.Raw + "providerStatus", "Too small: expected number to be >0");
        if (value.CollectedAmount is { } amount)
        {
            if (amount.ValueKind != JsonValueKind.Number) issue(ValidationPaths.Raw + "collectedAmount", Zod.Expected("number", amount.ValueKind == JsonValueKind.String ? "string" : "object"));
            else if (amount.GetDouble() < 0) issue(ValidationPaths.Raw + "collectedAmount", Zod.TooSmallNumber(0));
            else if (amount.GetDouble() > 1_000_000) issue(ValidationPaths.Raw + "collectedAmount", Zod.TooBigNumber(1_000_000));
        }
    }
}

internal sealed class ProviderStatusHandler(IAppDbContext db, ICurrentUser caller, IClock clock, ILogger<ProviderStatusHandler> logger) : IRequestHandler<ProviderStatusCommand, ProviderStatusResultDto>
{
    public Task<ProviderStatusResultDto> Handle(ProviderStatusCommand request, CancellationToken cancellationToken) =>
        ProviderStatusReport.HandleAsync(db, caller, clock, logger, request, cancellationToken);
}
