using System.Text.Json.Serialization;

namespace Delivery.Application.Pricing.Dtos;

/// <summary>`PricingRuleListItem` : tarifs en centimes.</summary>
public sealed record PricingRuleListItemDto(
    string Id, string OriginCityId, string OriginCityName, string DestinationCityId, string DestinationCityName,
    long RateDistrict, long RateAgency, long RateRelayPoint, bool IsEnabled);

/// <summary>`PricingOriginRow` : une destination de la grille d'une origine.</summary>
public sealed record PricingOriginRowDto(string CityId, string Name, string? NameAr, string Code, string ZoneId, string ZoneName, long? Rate, long ZoneRate, string? DeliveryDelay);

public sealed record OriginRefDto(string Id, string Name, string Code);

/// <summary>`PricingOriginPage`.</summary>
public sealed record PricingOriginPageDto(IReadOnlyList<PricingOriginRowDto> Items, int Total, int Page, int PageSize, OriginRefDto Origin, int Priced);

public sealed record PricingOriginSaveResult(int Priced, int Cleared, int Delays);

/// <summary>`ImportRowError`.</summary>
public sealed record ImportRowError(
    int Row,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Column,
    string Code,
    string Message);

/// <summary>`PricingOriginImportRow`.</summary>
public sealed record PricingOriginImportRowDto(
    int Row, string Raw, string CityId, string Name, string Code, long? Rate, long? PreviousRate, string? DeliveryDelay, string? PreviousDelay, bool Changed);

/// <summary>`PricingOriginImportPreview`.</summary>
public sealed record PricingOriginImportPreviewDto(OriginRefDto Origin, int Rows, IReadOnlyList<PricingOriginImportRowDto> Recognized, IReadOnlyList<ImportRowError> Rejected, int Changes);

/// <summary>Ligne de `pricingOriginSaveSchema`.</summary>
public sealed record PricingOriginRowInput
{
    public string? DestinationCityId { get; init; }

    /// <summary>`rate` est `nullable()` mais pas facultatif : absent est une erreur.</summary>
    [JsonIgnore]
    public bool HasRate { get; private init; }

    public double? Rate
    {
        get;
        init
        {
            field = value;
            HasRate = true;
        }
    }

    /// <summary>Absent = délai inchangé ; présent (même `null`) = délai réécrit.</summary>
    [JsonIgnore]
    public bool HasDelay { get; private init; }

    public string? DeliveryDelay
    {
        get;
        init
        {
            field = value?.Trim();
            HasDelay = true;
        }
    }
}
