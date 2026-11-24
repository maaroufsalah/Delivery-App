namespace Delivery.Application.Common.Documents;

/// <summary>Une ligne de la feuille de route (`RouteSheetParcel`), montants en centimes.</summary>
public sealed record RouteSheetParcel(string Code, string RecipientName, string RecipientPhone, string DeliveryAddress, string? DestinationDistrictName, string SenderName, string SenderPhone,
    string PickupAddress, string OriginCityName, long CodToCollect, int Quantity);

/// <summary>`RouteSheetDocument` : la feuille que le livreur emporte (livraison, ramassage ou retour).</summary>
public sealed record RouteSheet(PdfCompany Company, string Code, string Kind, string CourierName, string CityName, DateTime? ExitDate, IReadOnlyList<RouteSheetParcel> Parcels, int? DoneCount,
    int? RemovedCount);

/// <summary>Une ligne du bon (`DeliveryNoteLine`), montants en centimes.</summary>
public sealed record DeliveryNoteSheetLine(string Code, string RecipientName, string DestinationCityName, string? DestinationDistrictName, long CodAmount, long DeliveryFee, int Quantity,
    string? Correction);

/// <summary>`DeliveryNoteDocument` : le bon de ramassage ou de dépôt.</summary>
public sealed record DeliveryNoteSheet(PdfCompany Company, string Code, string Kind, string ClientName, string AgencyName, string CityName, DateTime CreatedAt, DateTime? ValidatedAt,
    bool PickupWaived, IReadOnlyList<DeliveryNoteSheetLine> Parcels, int? PickedCount, int? RemovedCount);

/// <summary>`cartons.ts` : expéditions et cartons d'une liste.</summary>
public static class Shipments
{
    public const string ColumnFr = "Expédition";

    public static int CartonsOf(int quantity) => quantity > 1 ? quantity : 1;

    public static bool ShowsCartons(IEnumerable<int> quantities) => quantities.Any(q => CartonsOf(q) > 1);

    /// <summary>`shipmentTotalFr` : « 3 expéditions · 7 cartons », ou « 3 expéditions ».</summary>
    public static string TotalFr(IReadOnlyList<int> quantities)
    {
        var count = quantities.Count;
        var shipments = $"{count} {(count == 1 ? "expédition" : "expéditions")}";
        if (!ShowsCartons(quantities)) return shipments;
        var cartons = quantities.Sum(CartonsOf);
        return $"{shipments} · {cartons} {(cartons == 1 ? "carton" : "cartons")}";
    }
}
