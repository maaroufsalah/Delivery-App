using System.Globalization;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Finance;
using Delivery.Application.Parcels;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using Delivery.Domain.Parcels;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Invoices;

public class InvoiceListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public bool ClientIsPro { get; set; }
    public string AgencyId { get; set; } = string.Empty;
    public string AgencyName { get; set; } = string.Empty;
    public int ParcelCount { get; set; }
    public long TotalCod { get; set; }
    public long TotalDeliveryFee { get; set; }
    public long TotalFailureFee { get; set; }
    public long TotalAdjustment { get; set; }
    public long TotalVat { get; set; }
    public int VatRate { get; set; }
    public int? ProVatRate { get; set; }
    public long TotalDue { get; set; }
    public string BillingType { get; set; } = string.Empty;
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    public long TotalPaid { get; set; }

    /// <summary>Reportée : la facture qui a absorbé ce net négatif ; `null` tant que le report attend.</summary>
    public string? CarriedIntoCode { get; set; }

    public DateTime? IssuedAt { get; set; }
    public DateTime? DueAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed record InvoiceLineDto(string Id, string ParcelId, string ParcelCode, string? RecipientName, string? ExternalCode, string? RecipientPhone, string? DestinationCityName,
    DateTime? DeliveredAt, int StatusId, long CodAmount, long DeliveryFee, long FailureFee, long VatAmount, long ChequePaid, string? ChequeNumber, bool ChequeRejected, long NetDue);

public sealed record InvoiceAdjustmentDto(string Id, string Kind, long Amount, long VatAmount, string Reason, string? Reference, string? CreatedByName, DateTime CreatedAt);

public sealed record PaymentListItemDto(string Id, string? InvoiceId, string? InvoiceCode, string ClientId, string ClientName, string AgencyId, string AgencyName, long Amount, string Method,
    string? BankAccountId, string? BankAccountLabel, string? Reference, string? Note, string? ChequeNumber, string? ChequeBank, DateTime? ChequeDate, string? ProofName, DateTime PaidAt,
    DateTime CreatedAt);

public sealed class InvoiceDetailDto : InvoiceListItemDto
{
    [JsonPropertyOrder(1)] public string? Note { get; set; }
    [JsonPropertyOrder(1)] public string? IssuedByName { get; set; }
    [JsonPropertyOrder(1)] public List<InvoiceLineDto> Lines { get; set; } = [];
    [JsonPropertyOrder(1)] public List<InvoiceAdjustmentDto> Adjustments { get; set; } = [];
    [JsonPropertyOrder(1)] public List<PaymentListItemDto> Payments { get; set; } = [];
    [JsonPropertyOrder(1)] public bool Issuable { get; set; }
    [JsonPropertyOrder(1)] public bool Adjustable { get; set; }
    [JsonPropertyOrder(1)] public long Balance { get; set; }
}

public sealed record BillableParcelLineDto(string Id, string Code, string ClientId, string ClientName, string RecipientName, string DestinationCityName, int StatusId, DateTime StatusAt,
    long CodAmount, long DeliveryFee, long? FailureFee, long VatAmount, long NetDue, bool CashReceived, string Billability);

public sealed record BulkInvoiceCandidateDto(string ClientId, string ClientName, string AgencyId, string AgencyName, int ParcelCount, long TotalDue);

public sealed record BulkInvoiceCreatedDto(string ClientId, string ClientName, string Code, int ParcelCount, bool Reused);

public sealed record BulkInvoiceSkippedDto(string ClientId, string ClientName, string Reason);

public sealed record BulkInvoiceResultDto(IReadOnlyList<BulkInvoiceCreatedDto> Created, IReadOnlyList<BulkInvoiceSkippedDto> Skipped);

public sealed record InvoiceDaySummaryDto(string Day, int InvoiceCount, int ParcelCount, long TotalDue, long PaidCash, long PaidCheque, long PaidTransfer, IReadOnlyList<string> TransferableCodes);

/// <summary>`TransferLine` : une ligne d'ordre de virement, par vendeur.</summary>
public sealed record TransferLineDto(string ClientId, string ClientName, int ParcelCount, long Collected, long Fees, long Due);
