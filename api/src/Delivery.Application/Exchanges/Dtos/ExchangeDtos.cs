using System.Text.Json.Serialization;

namespace Delivery.Application.Exchanges.Dtos;

/// <summary>`ExchangeListItem`.</summary>
public class ExchangeListItemDto
{
    public string Id { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string OriginalParcelId { get; set; } = string.Empty;

    public string OriginalParcelCode { get; set; } = string.Empty;

    public string? NewParcelId { get; set; }

    public string? NewParcelCode { get; set; }

    public string ClientName { get; set; } = string.Empty;

    public string ReturnedProduct { get; set; } = string.Empty;

    public string NewProduct { get; set; } = string.Empty;

    public string OldDestination { get; set; } = string.Empty;

    public string NewDestination { get; set; } = string.Empty;

    public long Amount { get; set; }

    public long Fees { get; set; }

    public string? CourierId { get; set; }

    public string? CourierName { get; set; }

    public DateTime RequestedAt { get; set; }

    public DateTime? PickedUpAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public DateTime? CancelledAt { get; set; }
}

/// <summary>`ExchangeDetail` : l'échange, son historique et les gestes permis.</summary>
public sealed class ExchangeDetailDto : ExchangeListItemDto
{
    [JsonPropertyOrder(1)]
    public string? Note { get; set; }

    [JsonPropertyOrder(1)]
    public List<ExchangeEventDto> Events { get; set; } = [];

    [JsonPropertyOrder(1)]
    public bool Pickupable { get; set; }

    [JsonPropertyOrder(1)]
    public bool Deliverable { get; set; }

    [JsonPropertyOrder(1)]
    public bool Closable { get; set; }

    [JsonPropertyOrder(1)]
    public bool Cancellable { get; set; }
}

public sealed record ExchangeEventDto(string Id, string Status, string? Comment, string? AuthorName, DateTime CreatedAt);
