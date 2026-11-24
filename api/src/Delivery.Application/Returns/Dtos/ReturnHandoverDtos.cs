using System.Globalization;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Returns;

/// <summary>`ReturnParcelLine` : un colis rapportable ou en attente de retour.</summary>
public sealed record ReturnParcelLineDto(string Id, string Code, string? ExternalCode, string ClientId, string ClientName, string RecipientName, string RecipientPhone, string DestinationCityName,
    string Description, int StatusId, long CodAmount, long DeliveryFee, string? CourierName, DateTime StatusAt, int Quantity);

public class ReturnHandoverListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public bool CourierCanEdit { get; set; }
    public string CourierId { get; set; } = string.Empty;
    public string CourierName { get; set; } = string.Empty;
    public string AgencyId { get; set; } = string.Empty;
    public string AgencyName { get; set; } = string.Empty;
    public int ParcelCount { get; set; }
    public List<string> ClientNames { get; set; } = [];
    public string? CreatedByName { get; set; }
    public DateTime? PickedUpAt { get; set; }
    public DateTime? HandedOverAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? ConfirmedByName { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed record ReturnHandoverItemDto(string Id, string ParcelId, string ParcelCode, string ClientName, string RecipientName, int StatusId, string? Note, DateTime ParcelCreatedAt, int Quantity,
    DateTime? CorrectedAt);

public sealed record ReturnHandoverStepDto(string Id, string Status, string? Comment, string? AuthorName, DateTime CreatedAt);

public sealed class ReturnHandoverDetailDto : ReturnHandoverListItemDto
{
    [JsonPropertyOrder(1)] public string? Note { get; set; }
    [JsonPropertyOrder(1)] public string? CancelReason { get; set; }
    [JsonPropertyOrder(1)] public bool Editable { get; set; }
    [JsonPropertyOrder(1)] public bool Requestable { get; set; }
    [JsonPropertyOrder(1)] public bool Pickable { get; set; }
    [JsonPropertyOrder(1)] public bool Handoverable { get; set; }
    [JsonPropertyOrder(1)] public bool Confirmable { get; set; }
    [JsonPropertyOrder(1)] public bool Closable { get; set; }
    [JsonPropertyOrder(1)] public bool Cancellable { get; set; }
    [JsonPropertyOrder(1)] public bool Deletable { get; set; }
    [JsonPropertyOrder(1)] public List<ReturnHandoverItemDto> Items { get; set; } = [];
    [JsonPropertyOrder(1)] public List<ReturnHandoverStepDto> Steps { get; set; } = [];
}

/// <summary>`ReturnSlipBulkSummary` : bons ouverts, propriétaires distincts, colis, codes.</summary>
public sealed record ReturnBulkSummaryDto(int Created, int Clients, int Parcels, IReadOnlyList<string> Codes);

public sealed record ReturnCodeDto(string Code);
