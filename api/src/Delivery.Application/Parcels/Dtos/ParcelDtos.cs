using System.Text.Json.Serialization;

namespace Delivery.Application.Parcels.Dtos;

/// <summary>
/// `Parcel` de `@delivery/shared` : montants en centimes, énumérations « fil ».
/// Les champs financiers internes ou vendeur sont posés dans <see cref="Extra"/>
/// selon qui regarde (`canSeeInternalCash`).
/// </summary>
public class ParcelDto
{
    public string Id { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? ExternalCode { get; set; }

    public string ClientId { get; set; } = string.Empty;

    public string? CourierId { get; set; }

    public string AgencyId { get; set; } = string.Empty;

    public int StatusId { get; set; }

    public DateTime StatusAt { get; set; }

    public string BillingState { get; set; } = string.Empty;

    public string OriginCityId { get; set; } = string.Empty;

    public string DestinationCityId { get; set; } = string.Empty;

    public string? DestinationDistrictId { get; set; }

    public string DeliveryTarget { get; set; } = string.Empty;

    public string CurrentCityId { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string RecipientName { get; set; } = string.Empty;

    public string RecipientPhone { get; set; } = string.Empty;

    public string PickupAddress { get; set; } = string.Empty;

    public string DeliveryAddress { get; set; } = string.Empty;

    public long CodAmount { get; set; }

    public long DeliveryFee { get; set; }

    public long? FailureFee { get; set; }

    public long OtherFees { get; set; }

    public bool FeeIncluded { get; set; }

    public bool AlreadyPaid { get; set; }

    public bool PickupWaived { get; set; }

    public string Kind { get; set; } = string.Empty;

    public bool IsReturn { get; set; }

    public bool IsFailed { get; set; }

    public string? ReferenceParcelId { get; set; }

    public bool OpenOnDelivery { get; set; }

    public bool Fragile { get; set; }

    public bool BlRequested { get; set; }

    public int Quantity { get; set; }

    public string? ExpectedPayment { get; set; }

    public DateTime? ScheduledAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?> Extra { get; set; } = [];
}

/// <summary>`ParcelListItem` : le colis et les libellés qui l'accompagnent dans une liste.</summary>
public class ParcelListItemDto : ParcelDto
{
    [JsonPropertyOrder(1)]
    public string ClientName { get; set; } = string.Empty;

    [JsonPropertyOrder(1)]
    public string? CourierName { get; set; }

    [JsonPropertyOrder(1)]
    public string OriginCityName { get; set; } = string.Empty;

    [JsonPropertyOrder(1)]
    public string DestinationCityName { get; set; } = string.Empty;

    [JsonPropertyOrder(1)]
    public string? DestinationDistrictName { get; set; }
}

public sealed record ParcelEventDto(string Id, string ParcelId, int StatusId, string? Comment, string? AuthorName, string? CityId, string? AttachmentName, DateTime? ScheduledDate, DateTime CreatedAt)
{
    /// <summary>`blStep` : vu du vendeur seulement.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? BlStep { get; init; }

    [JsonIgnore]
    public bool HasBlStep { get; init; }
}

public sealed record ParcelNoteDto(string Id, string ParcelId, string Body, string AuthorName, string? AttachmentName, DateTime CreatedAt);

/// <summary>`ParcelStats`.</summary>
public sealed record ParcelStatsDto(int Total, int Delivered, int InTransit, int Failed, long CollectedCod, double DeliveryRate)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ParcelStatsWindow? Window { get; init; }
}

public sealed record ParcelStatsWindow(string Period, string From, string To, string Granularity);
