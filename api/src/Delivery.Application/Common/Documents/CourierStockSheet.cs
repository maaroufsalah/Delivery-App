using Delivery.Domain.Parcels;

namespace Delivery.Application.Common.Documents;

public sealed record CourierStockSheetParcel(string Code, string RecipientName, string RecipientPhone, string DestinationCityName, string ClientName, int StatusId, string? RouteCode,
    long CodInCents);

public sealed record CourierStockSheetStatusLine(int StatusId, int Count, long CodInCents);

public sealed record CourierStockSheetSection(string CourierName, string? AgencyName, CourierStockCounters Counters, IReadOnlyList<CourierStockSheetStatusLine> ByStatus,
    IReadOnlyList<CourierStockSheetParcel> Parcels);

public sealed record CourierStockSheetRow(string CourierName, string? AgencyName, CourierStockCounters Counters);

public sealed record CourierStockSheetSummary(IReadOnlyList<CourierStockSheetRow> Rows, CourierStockCounters Totals, IReadOnlyList<string> Filters);

/// <summary>`CourierStockPdfData` : « Situation du Stock Livreur », un récapitulatif (global) puis une section par livreur.</summary>
public sealed record CourierStockSheet(PdfCompany Company, string? ShortName, byte[]? Logo, DateTime GeneratedAt, CourierStockSheetSummary? Summary,
    IReadOnlyList<CourierStockSheetSection> Sections)
{
    public const string Title = "Situation du Stock Livreur";
}
