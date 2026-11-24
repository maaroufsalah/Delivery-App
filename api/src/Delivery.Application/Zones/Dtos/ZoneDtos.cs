namespace Delivery.Application.Zones.Dtos;

/// <summary>`ZoneListItem` : tarifs en centimes.</summary>
public sealed record ZoneListItemDto(string Id, string Name, long BaseRate, long SameCityRate, int DeliveryDays, bool IsActive, IReadOnlyList<string> CityIds, int CityCount);

public sealed record ZoneRefDto(string Id, string Name);

/// <summary>`ZoneRateItem`.</summary>
public sealed record ZoneRateItemDto(string Id, string FromZoneId, string ToZoneId, long Rate, int DeliveryDays, bool IsActive);

/// <summary>`ZoneRateMatrix`.</summary>
public sealed record ZoneRateMatrixDto(IReadOnlyList<ZoneRefDto> Zones, IReadOnlyList<ZoneRateItemDto> Rates);
