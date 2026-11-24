using System.Globalization;
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
using Delivery.Application.PettyCash;
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

public class CourierStatementListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CourierId { get; set; } = string.Empty;
    public string CourierName { get; set; } = string.Empty;
    public string AgencyId { get; set; } = string.Empty;
    public string AgencyName { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string PeriodKind { get; set; } = string.Empty;
    public string PayModel { get; set; } = string.Empty;
    public string SettlementMode { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public long BaseAmount { get; set; }
    public long CodCollected { get; set; }
    public long CommissionsTotal { get; set; }
    public long CommissionsDeducted { get; set; }
    public long VariableAmount { get; set; }
    public long TotalDue { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public string? ValidatedByName { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed record CourierStatementLineDto(string Id, string ParcelId, string Kind, string ParcelCode, string CityName, int StatusId, long Amount, bool Deducted, DateTime WorkedAt);

public sealed class CourierStatementDetailDto : CourierStatementListItemDto
{
    [JsonPropertyOrder(1)] public string? Note { get; set; }
    [JsonPropertyOrder(1)] public bool Validatable { get; set; }
    [JsonPropertyOrder(1)] public string PaymentMethod { get; set; } = string.Empty;
    [JsonPropertyOrder(1)] public string? BankAccountId { get; set; }
    [JsonPropertyOrder(1)] public string? BankAccountLabel { get; set; }
    [JsonPropertyOrder(1)] public string? Reference { get; set; }
    [JsonPropertyOrder(1)] public string? ChequeNumber { get; set; }
    [JsonPropertyOrder(1)] public string? ChequeBank { get; set; }
    [JsonPropertyOrder(1)] public DateTime? ChequeDate { get; set; }
    [JsonPropertyOrder(1)] public string? ProofName { get; set; }
    [JsonPropertyOrder(1)] public List<CourierStatementLineDto> Lines { get; set; } = [];
}

/// <summary>`CourierCheque` : un relevé réglé par chèque, vu du livreur.</summary>
public sealed record CourierChequeDto(string Id, string StatementCode, string Number, string? Bank, DateTime? IssuedOn, long AmountInCents, bool HandedOver, bool HasPhoto);
