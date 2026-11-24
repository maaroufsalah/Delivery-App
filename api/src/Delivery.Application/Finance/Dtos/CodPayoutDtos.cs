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
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Application.PettyCash;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Finance;

public class CodPayoutListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string AgencyId { get; set; } = string.Empty;
    public string AgencyName { get; set; } = string.Empty;
    public int ParcelCount { get; set; }
    public long Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string? BankAccountId { get; set; }
    public string? BankAccountLabel { get; set; }
    public string? Reference { get; set; }
    public string? ProofName { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? PaidByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed record CodPayoutItemDto(string ParcelId, string ParcelCode, string RecipientName, string DestinationCityName, long Amount);

public sealed record CodPayoutChequeDto(string Id, string Number, string? Bank, long Amount, string ParcelCode, string Status, bool IncludedInPayout);

public sealed class CodPayoutDetailDto : CodPayoutListItemDto
{
    [JsonPropertyOrder(1)] public string? Note { get; set; }
    [JsonPropertyOrder(1)] public bool Payable { get; set; }
    [JsonPropertyOrder(1)] public List<CodPayoutItemDto> Items { get; set; } = [];
    [JsonPropertyOrder(1)] public List<CodPayoutChequeDto> Cheques { get; set; } = [];
}

public sealed record CodPayoutCandidateDto(string Id, string Code, string RecipientName, string DestinationCityName, long CodCollected, DateTime? ReimbursedAt, DateTime StatusAt);

public sealed record CodPayoutBulkCandidateDto(string ClientId, string ClientName, string AgencyId, string AgencyName, int ParcelCount, long Cash, int ChequeCount, long ChequeAmount, long Fees,
    long Total);

public sealed record CodPayoutBulkCreatedDto(string ClientId, string ClientName, string AgencyName, string Code, int ParcelCount, long Amount);

public sealed record CodPayoutBulkSkippedDto(string ClientId, string ClientName, string Reason);

public sealed record CodPayoutBulkResultDto(IReadOnlyList<CodPayoutBulkCreatedDto> Created, IReadOnlyList<CodPayoutBulkSkippedDto> Skipped, long Total);
