using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Finance;

public class RemittanceListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public DateTime? CourierClosedAt { get; set; }
    public string? CourierClosedByName { get; set; }
    public string CourierId { get; set; } = string.Empty;
    public string CourierName { get; set; } = string.Empty;
    public string CityId { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;
    public string AgencyId { get; set; } = string.Empty;
    public string AgencyName { get; set; } = string.Empty;
    public int ParcelCount { get; set; }
    public long Amount { get; set; }
    public long TotalCash { get; set; }
    public long TotalCheque { get; set; }
    public int ChequeCount { get; set; }
    public long ExpectedCod { get; set; }
    public long ExpectedCommission { get; set; }
    public long ExpectedNet { get; set; }
    public long? DeclaredAmount { get; set; }
    public long Variance { get; set; }
    public string? VarianceReason { get; set; }
    public string SettlementMode { get; set; } = string.Empty;
    public DateTime? ConfirmedAt { get; set; }
    public string? ConfirmedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed record RemittanceChequeDto(string Id, string Number, string? Bank, long Amount, string Status, string ParcelId, string ParcelCode);

public sealed record RemittanceItemDto(string ParcelId, string ParcelCode, string ClientName, string RecipientName, long Amount, long Cash, long Cheque, IReadOnlyList<RemittanceChequeDto> Cheques,
    long Commission);

public sealed class RemittanceDetailDto : RemittanceListItemDto
{
    [JsonPropertyOrder(1)] public string? Note { get; set; }
    [JsonPropertyOrder(1)] public bool Confirmable { get; set; }
    [JsonPropertyOrder(1)] public bool Reopenable { get; set; }
    [JsonPropertyOrder(1)] public List<RemittanceItemDto> Items { get; set; } = [];
    [JsonPropertyOrder(1)] public List<RemittanceChequeDto> Cheques { get; set; } = [];
}

public sealed record RemittanceCandidateDto(string Id, string Code, string ClientName, string RecipientName, string DestinationCityName, long CodCollected, long CourierCommission, DateTime StatusAt);

public sealed record RemittanceSummaryTotalsDto(long Collected, long Cash, long Cheque, long ToHandOver, long Handed, long Variance, int Count, int OpenCount, int ConfirmedCount);

public sealed record RemittanceSummaryOpenDto(string RemittanceId, string Code, string CourierId, string CourierName, string AgencyName, long ToHandOver, long ChequeAmount, int ChequeCount,
    int ParcelCount, DateTime OpenedAt, DateTime? CourierClosedAt);

public sealed record RemittanceSummaryDto(string Period, string From, string To, string? CourierId, RemittanceSummaryTotalsDto Totals, IReadOnlyList<RemittanceSummaryOpenDto> Open);

/// <summary>`CourierOpenRemittance` : enveloppé, car « pas de caisse ouverte » est une réponse, pas un corps vide.</summary>
public sealed record CourierOpenRemittanceDto([property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] RemittanceDetailDto? Remittance);

public sealed record CourierBalanceEntryDto(string Id, string Kind, long Amount, string? RemittanceCode, string? Method, string? Note, string AuthorName, DateTime At);

public sealed record CourierBalanceDto(string CourierId, string CourierName, long Balance, IReadOnlyList<CourierBalanceEntryDto> Entries);
