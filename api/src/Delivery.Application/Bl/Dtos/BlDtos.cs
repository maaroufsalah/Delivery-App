using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Bl;

public sealed class DeliverySlipListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ParcelId { get; set; } = string.Empty;
    public string ParcelCode { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string? CourierId { get; set; }
    public string? CourierName { get; set; }
    public string? Reference { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CollectedAt { get; set; }
    public DateTime? HandedOverAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public string? ReturnedToName { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed record DeliverySlipStepDto(string Id, string Status, string? Comment, string? AuthorName, DateTime CreatedAt);

public sealed class DeliverySlipDetailDto
{
    [JsonPropertyOrder(-1)] public DeliverySlipListItemDto Slip { get; set; } = new();
}

public sealed record ParcelBlTrackingDto(bool Requested, Dictionary<string, object?>? Slip);

public class BlNoteListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CourierId { get; set; } = string.Empty;
    public string CourierName { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string AgencyId { get; set; } = string.Empty;
    public string AgencyName { get; set; } = string.Empty;
    public int SlipCount { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime? HandedOverAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public string? ReturnedToName { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed record BlNoteStepDto(string Id, string Status, string? Comment, string? AuthorName, DateTime CreatedAt);

public sealed class BlNoteDetailDto : BlNoteListItemDto
{
    [JsonPropertyOrder(1)] public string? Note { get; set; }
    [JsonPropertyOrder(1)] public bool Editable { get; set; }
    [JsonPropertyOrder(1)] public bool Handoverable { get; set; }
    [JsonPropertyOrder(1)] public bool Receivable { get; set; }
    [JsonPropertyOrder(1)] public bool Returnable { get; set; }
    [JsonPropertyOrder(1)] public bool Closable { get; set; }
    [JsonPropertyOrder(1)] public List<DeliverySlipListItemDto> Items { get; set; } = [];
    [JsonPropertyOrder(1)] public List<BlNoteStepDto> Steps { get; set; } = [];
}

public sealed record DeliverySlipAdvanceResultDto(int Count, int Skipped);

public sealed record BlNoteCodeDto(string Code);
