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
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Finance;

public class ChequeListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string? Bank { get; set; }
    public string? ChequeDate { get; set; }
    public DateTime ReceivedAt { get; set; }
    public string PayerName { get; set; } = string.Empty;
    public string ParcelId { get; set; } = string.Empty;
    public string ParcelCode { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string CourierId { get; set; } = string.Empty;
    public string CourierName { get; set; } = string.Empty;
    public string? InvoiceCode { get; set; }
}

public sealed record ChequeEventDto(string Id, string Status, string Kind, string? Comment, string AuthorName, DateTime CreatedAt);

public sealed class ChequeDetailDto : ChequeListItemDto
{
    [JsonPropertyOrder(1)] public string? Note { get; set; }
    [JsonPropertyOrder(1)] public string? RejectionReason { get; set; }
    [JsonPropertyOrder(1)] public List<ChequeEventDto> Events { get; set; } = [];
    [JsonPropertyOrder(1)] public string? NextStatus { get; set; }
    [JsonPropertyOrder(1)] public bool Rejectable { get; set; }
    [JsonPropertyOrder(1)] public bool Cancellable { get; set; }
}

/// <summary>`ChequePage` : la page, et la somme de tous les chèques filtrés.</summary>
public sealed record ChequePageDto(IReadOnlyList<ChequeListItemDto> Items, int Total, int Page, int PageSize, long TotalAmount);

public sealed record ChequeBulkSkipDto(string Id, string? Number, string Reason, string? Status);

public sealed record ChequeBulkResultDto(IReadOnlyList<string> Done, IReadOnlyList<ChequeBulkSkipDto> Skipped);
