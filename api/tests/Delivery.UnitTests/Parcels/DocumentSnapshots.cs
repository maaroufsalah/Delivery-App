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

    private static byte[]? Bytes(string? dataUri) => dataUri is null ? null : Convert.FromBase64String(dataUri[(dataUri.IndexOf(',') + 1)..]);
}
