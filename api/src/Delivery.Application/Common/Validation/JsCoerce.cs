using System.Globalization;
using System.Text.Json;

namespace Delivery.Application.Common.Validation;

/// <summary>`z.coerce.number()` : `Number(valeur)` de JS sur un champ JSON quelconque.</summary>
public static class JsCoerce
{
    public static double Number(JsonElement? value)
    {
        if (value is null) return double.NaN;
        var element = value.Value;
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return element.GetDouble();
            case JsonValueKind.Null:
            case JsonValueKind.False:
                return 0;
            case JsonValueKind.True:
                return 1;
            case JsonValueKind.String:
                var text = element.GetString()!.Trim();
                if (text.Length == 0) return 0;
                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : double.NaN;
            case JsonValueKind.Array:
                return element.GetArrayLength() switch
                {
                    0 => 0,
                    1 => Number(element[0]),
                    _ => double.NaN,
                };
            default:
                return double.NaN;
        }
    }

    public static string? NumberIssue(double value, double min, double max, string minMessage, string maxMessage)
    {
        if (double.IsNaN(value)) return "Invalid input: expected number, received NaN";
        if (Math.Floor(value) != value || double.IsInfinity(value)) return Zod.ExpectedInt;
        if (value < min) return minMessage;
        if (value > max) return maxMessage;
        return null;
    }
}
