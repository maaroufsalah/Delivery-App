using System.Globalization;
using System.Text.RegularExpressions;

namespace Delivery.Domain.Common;

/// <summary>Couleurs HEX et contraste WCAG (`@delivery/shared/themes.ts`).</summary>
public static class Colors
{
    public const double MinContrast = 4.5;

    private static readonly Regex HexPattern = new("^#(?:[0-9a-f]{3}|[0-9a-f]{6})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsHex(string? value) => value is not null && HexPattern.IsMatch(value.Trim());

    /// <summary>`#abc` → `#AABBCC` ; <paramref name="fallback"/> si invalide.</summary>
    public static string NormalizeHex(string value, string fallback)
    {
        if (!IsHex(value)) return fallback;
        var hex = value.Trim()[1..];
        var full = hex.Length == 3 ? string.Concat(hex.Select(c => $"{c}{c}")) : hex;
        return "#" + full.ToUpperInvariant();
    }

    public static double ContrastRatio(string a, string b)
    {
        var first = Luminance(a);
        var second = Luminance(b);
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }

    private static double Luminance(string hex)
    {
        var value = NormalizeHex(hex, "#000000")[1..];
        double Channel(int start)
        {
            var ratio = int.Parse(value.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
            return ratio <= 0.03928 ? ratio / 12.92 : Math.Pow((ratio + 0.055) / 1.055, 2.4);
        }
        return (0.2126 * Channel(0)) + (0.7152 * Channel(2)) + (0.0722 * Channel(4));
    }
}
