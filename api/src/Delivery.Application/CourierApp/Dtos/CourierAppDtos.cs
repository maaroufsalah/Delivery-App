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

/// <summary>`proofPhotoSchema` : une photo de preuve sur la dernière issue posée par ce livreur.</summary>
public sealed record ProofPhotoDto
{
    public string? Name { get; init => field = Zod.Trim(value); }
    public string? MimeType { get; init; }
    public string? Content { get; init; }
}

public sealed record ProofAttachedDto(bool Attached);

public sealed record ScanResultDto(CourierRouteDto Route, CourierRouteParcelDto Added, bool RouteCreated)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Transferred { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReroutedTo { get; init; }
}

public sealed record CourierDeliveryRateDto(int Delivered, int Total, int Percent);

public sealed record CourierPerformanceDto(string Period, int ToPickup, int ToDeliver, int Delivered, int Returning, int Failed, long CollectedInCents, long ToRemitInCents, CourierDeliveryRateDto DeliveryRate);

public sealed record CourierPickupNoteDto(string Id, string Code, string Kind, string Status, string ClientName, string ClientPhone, string Address, string CityName, int ParcelCount, int PickedCount,
    int RemovedCount, DateTime CreatedAt, DateTime? ValidatedAt);
