using System.Text.Json.Serialization;

namespace Delivery.Application.Parcels.Dtos;

/// <summary>`ParcelDetail` : la fiche complète d'un colis.</summary>
public class ParcelDetailDto : ParcelListItemDto
{
    [JsonPropertyOrder(2)]
    public DateTime? DeletedAt { get; set; }

    [JsonPropertyOrder(2)]
    public long ReturnFee { get; set; }

    [JsonPropertyOrder(2)]
    public string AgencyName { get; set; } = string.Empty;

    [JsonPropertyOrder(2)]
    public long CodToCollect { get; set; }

    [JsonPropertyOrder(2)]
    public ParcelCollectedDto? Collected { get; set; }

    [JsonPropertyOrder(2)]
    public List<object> Events { get; set; } = [];

    [JsonPropertyOrder(2)]
    public List<ParcelNoteDto> Notes { get; set; } = [];

    [JsonPropertyOrder(2)]
    public List<ParcelFeeChangeDto> FeeChanges { get; set; } = [];

    [JsonPropertyOrder(2)]
    public List<ParcelExchangeLinkDto> Exchanges { get; set; } = [];

    [JsonPropertyOrder(2)]
    public List<ParcelFieldChangeDto> FieldChanges { get; set; } = [];

    [JsonPropertyOrder(2)]
    public List<ParcelRouteLinkDto> Routes { get; set; } = [];

    [JsonPropertyOrder(2)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ParcelFinanceViewDto? Finance { get; set; }

    [JsonPropertyOrder(2)]
    public string? InvoicedOn { get; set; }

    [JsonPropertyOrder(2)]
    public string? InvoiceSettledOn { get; set; }

    [JsonPropertyOrder(2)]
    public List<ParcelItemDto> Items { get; set; } = [];
}

/// <summary>Réponse de `PATCH /parcels/:code/destination` : la fiche, les circuits quittés, le nouveau tarif.</summary>
public sealed class ParcelDestinationResultDto : ParcelDetailDto
{
    [JsonPropertyOrder(3)]
    public List<string> DroppedFromRoutes { get; set; } = [];

    [JsonPropertyOrder(3)]
    public long? RepricedFrom { get; set; }

    [JsonPropertyOrder(3)]
    public long? RepricedTo { get; set; }
}

public sealed record ParcelCollectedDto(long Amount, long Cash, long Cheque);

/// <summary>Événement vu du vendeur : sans commentaire interne, avec l'étape BL.</summary>
public sealed record ParcelMerchantEventDto(string Id, string ParcelId, int StatusId, string? Comment, string? AuthorName, string? CityId, string? AttachmentName, DateTime? ScheduledDate, DateTime CreatedAt, string? BlStep);

public sealed record ParcelFeeChangeDto(string Id, string ParcelId, string Field, double? OldAmount, double NewAmount, string Reason, string AuthorName, DateTime CreatedAt);

public sealed record ParcelExchangeLinkDto(string ExchangeId, string ExchangeCode, string Status, string Role, string? CounterpartCode, string ReturnedProduct, string NewProduct, DateTime RequestedAt, string? ReturnParcelCode);

public sealed class ParcelFieldChangeDto
{
    public string Id { get; set; } = string.Empty;

    public string Entity { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string Field { get; set; } = string.Empty;

    public string? OldValue { get; set; }

    public string NewValue { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public string AuthorName { get; set; } = string.Empty;

    public DateTime At { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?> Extra { get; set; } = [];
}

public sealed record ParcelRouteLinkDto(string RouteId, string Code, string Kind, string Status, string CourierName, string CityName, DateTime? ExitDate, bool Confirmed, DateTime? ConfirmedAt, int? FinalStatusId);

public sealed record ParcelItemDto(string ProductId, string ProductName, string? ProductReference, int Quantity);

public sealed record ParcelFinanceViewDto(
    long ToCollect,
    long? Collected,
    long? Cash,
    long? Cheque,
    string? ChequeNumber,
    string? ChequeStatus,
    long? Commission,
    string? CommissionSource,
    string? RemittanceId,
    string? RemittanceCode,
    string? RemittanceStatus,
    string? InvoiceCode,
    string? InvoiceStatus,
    DateTime? InvoiceIssuedAt,
    DateTime? InvoicePaidAt,
    long? InvoiceNetDue,
    string? PayoutCode,
    DateTime? PayoutPaidAt,
    long? ReimbursedAmount);
