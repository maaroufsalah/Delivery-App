using System.Globalization;
using System.Text;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Interfaces;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Delivery.Infrastructure.Documents;

/// <summary>Rendu QuestPDF des documents de l'application (étiquettes, puis bons et factures).</summary>
public sealed class PdfService : IPdfService
{
    public const string Latin = "Noto Sans";
    public const string Arabic = "Noto Sans Arabic";

    private static readonly Lazy<bool> Fonts = new(() =>
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var assembly = typeof(PdfService).Assembly;
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".ttf", StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            QuestPDF.Drawing.FontManager.RegisterFontFromStream(stream);
        }
        return true;
    });

    public byte[] Render(object document)
    {
        _ = Fonts.Value;
        return document switch
        {
            LabelSheet sheet => LabelDocuments.Render(sheet),
            StorageReceipt receipt => BusinessDocuments.StorageReceipt(receipt),
            RouteSheet route => RouteDocumentsPdf.RouteSheet(route),
            DeliveryNoteSheet note => RouteDocumentsPdf.DeliveryNote(note),
            CourierStockSheet stock => CourierStockPdf.Render(stock),
            _ => throw new NotSupportedException($"Document PDF inconnu : {document.GetType().Name}"),
        };
    }

    internal static TextStyle Base(float size) => TextStyle.Default.FontFamily(Latin, Arabic).FontSize(size).FontColor("#000000");

    internal static byte[] Qr(string content)
    {
        using var generator = new QRCodeGenerator();
        // `qrMatrix` de Speedy : niveau M, toujours en mode octet (QRCoder choisirait l'alphanumérique, plus petit).
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M, forceUtf8: true);
        return new PngByteQRCode(data).GetGraphic(8);
    }

    /// <summary>
    /// `barcodeDataUri` : Code 128 en PNG, 10 modules de marge de chaque côté, 2 px par module ; l'image est étirée
    /// à la taille de sa case (`FitUnproportionally`), comme `Image` de react-pdf.
    /// </summary>
    internal static byte[] Barcode(string content, int scale = 2, int height = 60, int quiet = 10)
    {
        var modules = Code128B(content);
        var width = (modules.Length + quiet * 2) * scale;
        using var bitmap = new SkiaSharp.SKBitmap(width, height, SkiaSharp.SKColorType.Gray8, SkiaSharp.SKAlphaType.Opaque);
        bitmap.Erase(SkiaSharp.SKColors.White);
        using (var canvas = new SkiaSharp.SKCanvas(bitmap))
        using (var ink = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.Black, IsAntialias = false })
        {
            for (var i = 0; i < modules.Length; i++)
            {
                if (modules[i]) canvas.DrawRect((i + quiet) * scale, 0, scale, height, ink);
            }
        }
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    private static readonly string[] Code128Patterns =
    [
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213", "221312", "231212", "112232", "122132", "122231", "113222",
        "123122", "123221", "223211", "221132", "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211", "212123", "212321",
        "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313", "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121",
        "313121", "211331", "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111", "314111", "221411", "431111", "111224",
        "111422", "121124", "121421", "141122", "141221", "112214", "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141", "214121", "412121", "111143", "111341", "131141", "114113",
        "114311", "411113", "411311", "113141", "114131", "311141", "411131", "211412", "211214", "211232", "233111",
    ];

    /// <summary>`code128Modules` de Speedy : toujours le jeu B (un encodeur générique passerait au jeu C sur les chiffres).</summary>
    internal static bool[] Code128B(string text)
    {
        const int startB = 104, stop = 106;
        var values = text.Select(c => c >= 32 && c <= 126 ? c - 32 : '?' - 32).ToList();
        var checksum = startB;
        for (var i = 0; i < values.Count; i++) checksum += values[i] * (i + 1);
        var modules = new List<bool>();
        foreach (var index in new[] { startB }.Concat(values).Append(checksum % 103).Append(stop))
        {
            var pattern = Code128Patterns[index];
            for (var position = 0; position < pattern.Length; position++)
            {
                for (var i = 0; i < pattern[position] - '0'; i++) modules.Add(position % 2 == 0);
            }
        }
        modules.Add(true);
        modules.Add(true);
        return [.. modules];
    }

    /// <summary>`labelTint` : la couleur de marque mêlée de blanc (12 % de couleur).</summary>
    internal static string Tint(string color, double share = 0.12)
    {
        var hex = NormalizeHex(color);
        int Channel(int offset) => int.Parse(hex.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        string Mix(int value) => ((int)Math.Round(value * share + 255 * (1 - share))).ToString("X2", CultureInfo.InvariantCulture);
        return $"#{Mix(Channel(1))}{Mix(Channel(3))}{Mix(Channel(5))}";
    }

    private static string NormalizeHex(string color)
    {
        var text = color.Trim();
        if (text.Length == 4 && text[0] == '#') text = $"#{text[1]}{text[1]}{text[2]}{text[2]}{text[3]}{text[3]}";
        return text.Length == 7 && text[0] == '#' && text.Skip(1).All(Uri.IsHexDigit) ? text.ToUpperInvariant() : "#111111";
    }
}
