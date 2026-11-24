using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.CourierApp;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using Delivery.Domain.Pricing;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Returns;

public class ReturnSlipListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string AgencyId { get; set; } = string.Empty;
    public string AgencyName { get; set; } = string.Empty;
    public int ParcelCount { get; set; }
    public long TotalFees { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? ReceivedByName { get; set; }
    public string? DeliveredByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed record ReturnSlipItemDto(string Id, string ParcelId, string ParcelCode, string RecipientName, string DestinationCityName, string Reason, long FeeApplied, int StatusId,
    DateTime ParcelCreatedAt, int Quantity);

public sealed class ReturnSlipDetailDto : ReturnSlipListItemDto
{
    [JsonPropertyOrder(1)] public string? Note { get; set; }
    [JsonPropertyOrder(1)] public bool Closable { get; set; }
    [JsonPropertyOrder(1)] public List<ReturnSlipItemDto> Items { get; set; } = [];
}

/// <summary>`ReturnPendingGroup` : les colis à rapporter d'un livreur (ou sans livreur).</summary>
public sealed record ReturnPendingGroupDto(string? CourierId, string? CourierName, string? AgencyId, string? AgencyName, IReadOnlyList<ReturnParcelLineDto> Parcels);
