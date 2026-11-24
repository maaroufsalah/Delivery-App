using System.Text.Json.Serialization;
using Delivery.Application.Parcels.Dtos;
using Delivery.Application.Staff;

namespace Delivery.Application.Dashboard;

/// <summary>`ResolvedDashboardWindow` : dates locales et bornes UTC de la fenêtre.</summary>
public sealed record DashboardWindowDto(string Period, string From, string To, string Granularity, DateTime StartAt, DateTime EndAt);

public sealed record DashboardPreviousWindowDto(string From, string To);

/// <summary>`DashboardParcelCounts`.</summary>
public sealed record DashboardCountsDto(int Total, int Delivered, int InProgress, int InTransit, int Returned, int Failed, int Closed, double? SuccessRate, double? ReturnRate);

/// <summary>`DashboardOverview.kpis` : les compteurs, plus le jour, les livrés et le COD à remettre.</summary>
public sealed record DashboardKpisDto(
    int Total, int Delivered, int InProgress, int InTransit, int Returned, int Failed, int Closed, double? SuccessRate, double? ReturnRate,
    int TodayCount, int DeliveredCount, long CodToRemit);

/// <summary>`DashboardDayPoint` : un seau et ses colis par famille de statut.</summary>
public sealed class DashboardDayPointDto
{
    public string Date { get; init; } = string.Empty;
    public int Initial { get; set; }
    public int Transit { get; set; }
    public int Pivot { get; set; }
    public int Retry { get; set; }
    public int Failure { get; set; }
    public int Return { get; set; }
    public int Terminal { get; set; }
}

public sealed record DashboardCityPointDto(string CityId, string CityName, int Count);

public sealed record DashboardOverviewAlertsDto(IReadOnlyList<StuckParcelAlertDto> StuckInRetry, IReadOnlyList<StaleRouteAlertDto> StaleRoutes);

public sealed record DashboardOverviewDto(
    DashboardWindowDto Window, DashboardPreviousWindowDto? PreviousWindow, DashboardKpisDto Kpis, DashboardCountsDto? Previous,
    IReadOnlyList<DashboardDayPointDto> Daily, IReadOnlyList<DashboardCityPointDto> ByCity, IReadOnlyList<ParcelListItemDto> Latest, DashboardOverviewAlertsDto Alerts);

/// <summary>`DashboardSeriesPoint` : livrés, retours, en cours, échecs.</summary>
public sealed class DashboardSeriesPointDto
{
    public string Date { get; init; } = string.Empty;
    public int Delivered { get; set; }
    public int Returns { get; set; }
    public int InProgress { get; set; }
    public int Failures { get; set; }
}

public sealed record DashboardSeriesDto(string Range, string Granularity, string From, string To, DateTime StartAt, DateTime EndAt, IReadOnlyList<DashboardSeriesPointDto> Points);

public sealed record DashboardCourierRowDto(
    int Rank, string CourierId, string CourierName, int Assigned, int Delivered, int Returned, double? SuccessRate, double? AvgDelayHours, long CodCollected, int? PreviousDelivered);

public sealed record DashboardCityRowDto(string CityId, string CityName, int Total, int Delivered, int Returned, double? SuccessRate, double? ReturnRate, long CodCollected);

public sealed record DashboardClientRowDto(int Rank, string ClientId, string ClientName, bool IsPro, int Total, int Delivered, int Returned, double? SuccessRate, long CodCollected);

public sealed record DashboardMostActiveClientDto(string ClientId, string ClientName, int Current, int? Previous);

public sealed record DashboardClientsDto(string Rank, IReadOnlyList<DashboardClientRowDto> Items, int Total, int Page, int PageSize, DashboardMostActiveClientDto? MostActive);

public sealed record DashboardClientTotalsDto(
    string ClientId, string ClientName, bool IsPro, int Total, int Delivered, int Returned, double? SuccessRate, double? ReturnRate, long CodCollected, long CodReversed,
    double? AvgDelayHours);

public sealed record DashboardClientPreviousDto(int Total, int Delivered, int Returned);

public sealed record DashboardClientDetailDto(
    string ClientId, string ClientName, bool IsPro, DashboardWindowDto Window, DashboardClientTotalsDto Totals, DashboardClientPreviousDto? Previous,
    IReadOnlyList<DashboardSeriesPointDto> Series, string Granularity, IReadOnlyList<ParcelListItemDto> Latest);

/// <summary>`DashboardMoneySplit` : total, espèces, chèques (centimes).</summary>
public sealed record DashboardMoneySplitDto(long Total, long Cash, long Cheque);

public sealed record DashboardToRemitDto(DashboardMoneySplitDto Couriers, DashboardMoneySplitDto Remitted);

public sealed record DashboardLateCodDto(int Count, long Amount, double ThresholdHours);

public sealed record DashboardCountAmountDto(int Count, long Amount);

public sealed record DashboardFinancePreviousDto(long Collected, long Reversed, long Settlements, long TransferOrders, long CashVariance);

public sealed record DashboardFinanceDto(
    DashboardMoneySplitDto Collected, long Reversed, DashboardToRemitDto ToRemit, DashboardLateCodDto Late, DashboardCountAmountDto Settlements,
    DashboardCountAmountDto TransferOrders, DashboardCountAmountDto CashVariance, long CourierBalance, DashboardFinancePreviousDto? Previous);

public sealed record DashboardRoutesPreviousDto(int Closed, double? AvgDurationHours);

public sealed record DashboardRoutesDto(
    int Open, int Closed, int Late24, int Late48, double? AvgParcelsPerRoute, double? AvgDeliveredPerRoute, double? AvgReturnsPerRoute, long? AvgCodPerRoute,
    double? AvgDurationHours, DashboardRoutesPreviousDto? Previous);

public sealed class DashboardDelayPointDto
{
    public string Date { get; init; } = string.Empty;
    public double? AvgHours { get; set; }
    public int Count { get; set; }
}

public sealed record DashboardDelaysPreviousDto(double? AvgHours, double? Within24, double? Within48, double? Over48);

public sealed record DashboardDelaysDto(
    int Delivered, int Measured, int WithoutPickup, double? AvgHours, double? MedianHours, double? Within24, double? Within48, double? Over48, string Granularity,
    IReadOnlyList<DashboardDelayPointDto> Series, DashboardDelaysPreviousDto? Previous);

public sealed record CourierRateAlertDto(string CourierId, string CourierName, int Delivered, int Returned, double Rate);

public sealed record CodNotRemittedAlertDto(string CourierId, string CourierName, int Count, long Amount, long OldestHours);

public sealed record StuckParcelAgeAlertDto(string Code, string RecipientName, int StatusId, long Days);

public sealed record PerformanceDropAlertDto(string CourierId, string CourierName, double CurrentRate, double PreviousRate);

public sealed record CityFailureAlertDto(string CityId, string CityName, int Total, int Failed, double Rate);

/// <summary>`StaffAlertGroup &amp; { amount }` du COD non remonté.</summary>
public sealed record CodNotRemittedGroupDto(int Count, long Amount, IReadOnlyList<CodNotRemittedAlertDto> Items);

/// <summary>`DashboardAlertThresholds`.</summary>
public sealed record DashboardThresholdsDto(double CodLateHours, double StuckParcelDays, double CourierReturnRate, double CityFailureRate, double PerformanceDropPts, double MinParcels);

/// <summary>`DashboardAlerts` : les blocs financiers sont absents sans `finance.read`.</summary>
public sealed class DashboardAlertsDto
{
    public required StaffAlertGroupDto<CourierRateAlertDto> CourierReturnRate { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CodNotRemittedGroupDto? CodNotRemitted { get; init; }

    public required StaffAlertGroupDto<StaleRouteAlertDto> StaleRoutes { get; init; }

    public required StaffAlertGroupDto<StuckParcelAgeAlertDto> StuckParcels { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StaffAlertGroupDto<RemittanceAlertDto>? CashVariances { get; init; }

    public required StaffAlertGroupDto<PerformanceDropAlertDto> PerformanceDrops { get; init; }

    public required StaffAlertGroupDto<CityFailureAlertDto> CityFailureRate { get; init; }

    public required DashboardThresholdsDto Thresholds { get; init; }

    public required DateTime GeneratedAt { get; init; }
}
