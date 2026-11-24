namespace Delivery.Application.Cities.Dtos;

/// <summary>`CityListItem`.</summary>
public sealed class CityListItemDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? NameAr { get; set; }

    public string Code { get; set; } = string.Empty;

    public string ZoneId { get; set; } = string.Empty;

    public string ZoneName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int DistrictCount { get; set; }

    public int ParcelCount { get; set; }

    public bool CodeLocked { get; set; }
}
