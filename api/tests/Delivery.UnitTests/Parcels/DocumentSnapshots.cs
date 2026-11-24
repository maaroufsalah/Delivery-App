using System.Text.Json;
using Delivery.Application.Common.Documents;
using Delivery.Infrastructure.Documents;

namespace Delivery.UnitTests.Parcels;

/// <summary>
/// Rend les PDF .NET des mêmes données que les PDF Speedy de `docs/design/labels/` (comparaison visuelle).
/// Inerte sans la variable `DOCUMENT_SNAPSHOTS` (dossier de sortie) et `DOCUMENT_SAMPLE` (JSON d'exemple).
/// </summary>
public sealed class DocumentSnapshots
{
    [Fact]
    public void Render_label_snapshots_when_asked()
    {
        var outDir = Environment.GetEnvironmentVariable("DOCUMENT_SNAPSHOTS");
        var sampleFile = Environment.GetEnvironmentVariable("DOCUMENT_SAMPLE");
        if (string.IsNullOrEmpty(outDir) || string.IsNullOrEmpty(sampleFile)) return;
        using var sample = JsonDocument.Parse(File.ReadAllText(sampleFile));
        var options = new JsonSerializerOptions(JsonSerializerOptions.Web);
        var parcel = sample.RootElement.GetProperty("parcel").Deserialize<LabelData>(options)!;
        var two = sample.RootElement.GetProperty("two").Deserialize<LabelData>(options)!;
        var brandJson = sample.RootElement.GetProperty("brand");
        var brand = new LabelBrand(brandJson.GetProperty("name").GetString()!, brandJson.GetProperty("color").GetString()!, Bytes(brandJson.GetProperty("logo").GetString()), null)
        {
            OnBrandColor = brandJson.GetProperty("onBrandColor").GetString(),
            LogoOnLight = Bytes(brandJson.GetProperty("logoOnLight").GetString()),
        };
        var pdf = new PdfService();
        foreach (var format in new[] { LabelSheet.Format10X10, LabelSheet.FormatA4 })
        {
            File.WriteAllBytes(Path.Combine(outDir, $"dotnet-classic-{format}.pdf"), pdf.Render(new LabelSheet(LabelSheet.Classic, format, "Wimo Express", null, [parcel, two])));
            File.WriteAllBytes(Path.Combine(outDir, $"dotnet-branded-{format}.pdf"), pdf.Render(new LabelSheet(LabelSheet.Branded, format, "Wimo Express", brand, [parcel, two])));
        }
    }

    [Fact]
    public void Render_business_document_snapshots_when_asked()
    {
        var outDir = Environment.GetEnvironmentVariable("DOCUMENT_SNAPSHOTS");
        var sampleFile = Environment.GetEnvironmentVariable("DOCUMENT_BUSINESS_SAMPLE");
        if (string.IsNullOrEmpty(outDir) || string.IsNullOrEmpty(sampleFile)) return;
        using var sample = JsonDocument.Parse(File.ReadAllText(sampleFile));
        var c = sample.RootElement.GetProperty("company");
        string? S(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        var company = new PdfCompany(S(c, "name")!, S(c, "legalName"), S(c, "taxId"), S(c, "rc"), S(c, "address"), S(c, "city"), S(c, "phone"), S(c, "email"), S(c, "website"));
        var pdf = new PdfService();
        foreach (var (key, name) in new[] { ("receipt", "dotnet-storage-receipt"), ("refused", "dotnet-storage-receipt-refused") })
        {
            var r = sample.RootElement.GetProperty(key);
            var lines = r.GetProperty("items").EnumerateArray().Select(i => new StorageReceiptLine(S(i, "productReference"), S(i, "productName")!, i.GetProperty("quantity").GetInt32())).ToList();
            var receipt = new StorageReceipt(company, S(r, "code")!, S(r, "agencyName")!, S(r, "clientName")!, S(r, "status")!, DateTime.Parse(S(r, "decidedAt")!, null, System.Globalization.DateTimeStyles.AdjustToUniversal),
                S(r, "decidedByName"), S(r, "refusalReason"), lines, r.GetProperty("lineCount").GetInt32(), r.GetProperty("totalQuantity").GetInt32());
            File.WriteAllBytes(Path.Combine(outDir, $"{name}.pdf"), pdf.Render(receipt));
        }
        DateTime? Date(JsonElement e, string key) => S(e, key) is { } text ? DateTime.Parse(text, null, System.Globalization.DateTimeStyles.AdjustToUniversal) : null;
        int? Int(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;
        foreach (var (key, name) in new[] { ("route", "dotnet-route-sheet"), ("pickup", "dotnet-route-sheet-pickup") })
        {
            var r = sample.RootElement.GetProperty(key);
            var parcels = r.GetProperty("parcels").EnumerateArray().Select(p => new RouteSheetParcel(S(p, "code")!, S(p, "recipientName")!, S(p, "recipientPhone")!, S(p, "deliveryAddress")!,
                S(p, "destinationDistrictName"), S(p, "senderName")!, S(p, "senderPhone")!, S(p, "pickupAddress")!, S(p, "originCityName")!, p.GetProperty("codToCollect").GetInt64(),
                p.GetProperty("quantity").GetInt32())).ToList();
            File.WriteAllBytes(Path.Combine(outDir, $"{name}.pdf"), pdf.Render(new RouteSheet(company, S(r, "code")!, S(r, "kind")!, S(r, "courierName")!, S(r, "cityName")!, Date(r, "exitDate"),
                parcels, Int(r, "doneCount"), Int(r, "removedCount"))));
        }
        var n = sample.RootElement.GetProperty("note");
        var noteLines = n.GetProperty("parcels").EnumerateArray().Select(p => new DeliveryNoteSheetLine(S(p, "code")!, S(p, "recipientName")!, S(p, "destinationCityName")!, S(p, "destinationDistrictName"),
            p.GetProperty("codAmount").GetInt64(), p.GetProperty("deliveryFee").GetInt64(), p.GetProperty("quantity").GetInt32(), S(p, "correction"))).ToList();
        if (sample.RootElement.TryGetProperty("stock", out var stock))
        {
            Delivery.Domain.Parcels.CourierStockCounters Counters(JsonElement e) => new()
            {
                InHand = e.GetProperty("inHand").GetInt32(), PickedUp = e.GetProperty("pickedUp").GetInt32(), OutForDelivery = e.GetProperty("outForDelivery").GetInt32(),
                Awaiting = e.GetProperty("awaiting").GetInt32(), FailedOrReturning = e.GetProperty("failedOrReturning").GetInt32(), DeliveredUnremitted = e.GetProperty("deliveredUnremitted").GetInt32(),
                CollectedUnremittedInCents = e.GetProperty("collectedUnremittedInCents").GetInt64(), ToCollectInCents = e.GetProperty("toCollectInCents").GetInt64(),
            };
            var summaryJson = stock.GetProperty("summary");
            var summary = new CourierStockSheetSummary(
                summaryJson.GetProperty("rows").EnumerateArray().Select(r => new CourierStockSheetRow(S(r, "courierName")!, S(r, "agencyName"), Counters(r.GetProperty("counters")))).ToList(),
                Counters(summaryJson.GetProperty("totals")), summaryJson.GetProperty("filters").EnumerateArray().Select(f => f.GetString()!).ToList());
            var sections = stock.GetProperty("sections").EnumerateArray().Select(sec => new CourierStockSheetSection(S(sec, "courierName")!, S(sec, "agencyName"), Counters(sec.GetProperty("counters")),
                sec.GetProperty("byStatus").EnumerateArray().Select(l => new CourierStockSheetStatusLine(l.GetProperty("statusId").GetInt32(), l.GetProperty("count").GetInt32(), l.GetProperty("codInCents").GetInt64())).ToList(),
                sec.GetProperty("parcels").EnumerateArray().Select(p => new CourierStockSheetParcel(S(p, "code")!, S(p, "recipientName")!, S(p, "recipientPhone")!, S(p, "destinationCityName")!,
                    S(p, "clientName")!, p.GetProperty("statusId").GetInt32(), S(p, "routeCode"), p.GetProperty("codInCents").GetInt64())).ToList())).ToList();
            File.WriteAllBytes(Path.Combine(outDir, "dotnet-courier-stock.pdf"), pdf.Render(new CourierStockSheet(company, null, null, Date(stock, "generatedAt")!.Value, summary, sections)));
        }
        File.WriteAllBytes(Path.Combine(outDir, "dotnet-delivery-note.pdf"), pdf.Render(new DeliveryNoteSheet(company, S(n, "code")!, S(n, "kind")!, S(n, "clientName")!, S(n, "agencyName")!,
            S(n, "cityName")!, Date(n, "createdAt")!.Value, Date(n, "validatedAt"), n.GetProperty("pickupWaived").GetBoolean(), noteLines, Int(n, "pickedCount"), Int(n, "removedCount"))));
    }

    [Fact]
    public void Render_return_document_snapshots_when_asked()
    {
        var outDir = Environment.GetEnvironmentVariable("DOCUMENT_SNAPSHOTS");
        var sampleFile = Environment.GetEnvironmentVariable("DOCUMENT_RETURNS_SAMPLE");
        if (string.IsNullOrEmpty(outDir) || string.IsNullOrEmpty(sampleFile)) return;
        using var sample = JsonDocument.Parse(File.ReadAllText(sampleFile));
        string? S(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        DateTime? Date(JsonElement e, string key) => S(e, key) is { } text ? DateTime.Parse(text, null, System.Globalization.DateTimeStyles.AdjustToUniversal) : null;
        var c = sample.RootElement.GetProperty("company");
        var company = new PdfCompany(S(c, "name")!, S(c, "legalName"), S(c, "taxId"), S(c, "rc"), S(c, "address"), S(c, "city"), S(c, "phone"), S(c, "email"), S(c, "website"), S(c, "shortName"));
        var pdf = new PdfService();
        var n = sample.RootElement.GetProperty("note");
        File.WriteAllBytes(Path.Combine(outDir, "dotnet-bl-note.pdf"), pdf.Render(new BlNoteSheet(company, S(n, "code")!, S(n, "status")!, S(n, "agencyName")!, Date(n, "createdAt")!.Value,
            S(n, "clientName")!, S(n, "courierName")!, Date(n, "receivedAt"), Date(n, "handedOverAt"), S(n, "returnedToName"), n.GetProperty("slipCount").GetInt32(),
            n.GetProperty("items").EnumerateArray().Select(i => new BlNoteSheetLine(S(i, "parcelCode")!, S(i, "recipientName")!, S(i, "reference"))).ToList())));
        var h = sample.RootElement.GetProperty("handover");
        File.WriteAllBytes(Path.Combine(outDir, "dotnet-return-handover.pdf"), pdf.Render(new ReturnHandoverSheet(company, S(h, "code")!, S(h, "status")!, S(h, "origin")!, S(h, "agencyName")!,
            Date(h, "createdAt")!.Value, S(h, "courierName")!, S(h, "createdByName"), h.GetProperty("clientNames").EnumerateArray().Select(x => x.GetString()!).ToList(), Date(h, "confirmedAt"),
            Date(h, "pickedUpAt"), S(h, "confirmedByName"), h.GetProperty("parcelCount").GetInt32(),
            h.GetProperty("items").EnumerateArray().Select(i => new ReturnHandoverSheetLine(S(i, "parcelCode")!, S(i, "recipientName")!, S(i, "clientName")!, i.GetProperty("quantity").GetInt32(),
                i.GetProperty("statusId").GetInt32())).ToList())));
        var r = sample.RootElement.GetProperty("slip");
        File.WriteAllBytes(Path.Combine(outDir, "dotnet-return-slip.pdf"), pdf.Render(new ReturnSlipSheet(company, S(r, "code")!, S(r, "status")!, S(r, "agencyName")!, S(r, "clientName")!,
            Date(r, "deliveredAt"), S(r, "receivedByName"), r.GetProperty("parcelCount").GetInt32(), r.GetProperty("totalFees").GetInt64(),
            r.GetProperty("items").EnumerateArray().Select(i => new ReturnSlipSheetLine(S(i, "parcelCode")!, S(i, "recipientName")!, S(i, "reason")!, i.GetProperty("quantity").GetInt32(),
                i.GetProperty("feeApplied").GetInt64())).ToList())));
    }

    private static byte[]? Bytes(string? dataUri) => dataUri is null ? null : Convert.FromBase64String(dataUri[(dataUri.IndexOf(',') + 1)..]);
}
