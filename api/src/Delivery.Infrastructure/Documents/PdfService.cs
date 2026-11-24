using System.Globalization;
using System.Text;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Interfaces;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using ZXing.OneD;

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
            _ => throw new NotSupportedException($"Document PDF inconnu : {document.GetType().Name}"),
        };
    }

    internal static TextStyle Base(float size) => TextStyle.Default.FontFamily(Latin, Arabic).FontSize(size).FontColor("#000000");

    internal static byte[] Qr(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(8);
    }

    /// <summary>Code 128 en SVG étiré : une barre par module, sans marge.</summary>
    internal static string Barcode(string content)
    {
        var modules = new Code128Writer().encode(content);
        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {modules.Length} 10\" preserveAspectRatio=\"none\">");
        for (var i = 0; i < modules.Length; i++)
        {
            if (!modules[i]) continue;
            var start = i;
            while (i + 1 < modules.Length && modules[i + 1]) i++;
            svg.Append(CultureInfo.InvariantCulture, $"<rect x=\"{start}\" y=\"0\" width=\"{i - start + 1}\" height=\"10\" fill=\"#000\"/>");
        }
        svg.Append("</svg>");
        return svg.ToString();
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
