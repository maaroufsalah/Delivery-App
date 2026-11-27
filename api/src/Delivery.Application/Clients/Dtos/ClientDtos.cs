using System.Text.Json.Serialization;

namespace Delivery.Application.Clients.Dtos;

/// <summary>`ClientListItem` : énumérations au format « fil » (`merchant`, `professional`), montants en centimes.</summary>
public class ClientListItemDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? LegalName { get; set; }

    public string Type { get; set; } = string.Empty;

    public string? TaxId { get; set; }

    public string Phone { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string CityId { get; set; } = string.Empty;

    public string CityName { get; set; } = string.Empty;

    public string? AgencyId { get; set; }

    public string? AgencyName { get; set; }

    public bool IsActive { get; set; }

    public string BillingType { get; set; } = string.Empty;

    public bool IsPro { get; set; }

    public bool IsInternal { get; set; }

    public int? VatRate { get; set; }

    public long? PickupFee { get; set; }

    public int? FreePickupThreshold { get; set; }

    public bool MonthlyBilling { get; set; }

    public DateTime? ValidatedAt { get; set; }

    public string? ValidatedByName { get; set; }

    public DateTime? RejectedAt { get; set; }

    public string? RejectedByName { get; set; }

    public string? RejectionReason { get; set; }

    public string? UserId { get; set; }

    public int ParcelCount { get; set; }

    public DateTime CreatedAt { get; set; }
}

public sealed record ClientDiscountItemDto(string Id, IReadOnlyList<string> ZoneIds, IReadOnlyList<string> CityIds, long? FlatRate, long? SameCityRate, long? PickupFee, bool IsActive);

public sealed record ClientPickupAddressItemDto(string Id, string Label, string Address, string CityId, string CityName, string? DistrictId, string? DistrictName, string? Phone, bool IsDefault);

public sealed record ClientDocumentItemDto(string Id, string Name, string Kind, string MimeType, int SizeBytes, DateTime CreatedAt, string? UploadedByName);

/// <summary>`ClientDetail` : la fiche, ses remises, adresses de ramassage et documents.</summary>
public sealed class ClientDetailDto : ClientListItemDto
{
    [JsonPropertyOrder(1)]
    public string? Address { get; set; }

    [JsonPropertyOrder(1)]
    public string? Notes { get; set; }

    [JsonPropertyOrder(1)]
    public IReadOnlyList<ClientDiscountItemDto> Discounts { get; set; } = [];

    [JsonPropertyOrder(1)]
    public IReadOnlyList<ClientPickupAddressItemDto> PickupAddresses { get; set; } = [];

    [JsonPropertyOrder(1)]
    public IReadOnlyList<ClientDocumentItemDto> Documents { get; set; } = [];
}
