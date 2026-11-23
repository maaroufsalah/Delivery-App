using System.Text;
using System.Text.RegularExpressions;

namespace Delivery.Application.Common.Validation;

/// <summary>`@delivery/shared/phone.ts`.</summary>
public static class Phones
{
    public static readonly Regex Pattern = new("^[0-9+]+$", RegexOptions.Compiled);

    private const string Separators = " \t\r\n\f\v-_.()'’‘`";

    public static string Normalize(string phone)
    {
        var compact = new StringBuilder(phone.Length);
        foreach (var ch in phone)
        {
            if (Separators.Contains(ch) || char.IsWhiteSpace(ch)) continue;
            compact.Append(ch);
        }
        var text = compact.ToString();
        var leadingPlus = text.StartsWith('+');
        var body = text.Replace("+", string.Empty, StringComparison.Ordinal);
        if (leadingPlus) return "+" + body;
        if (body.StartsWith("00", StringComparison.Ordinal)) return "+" + body[2..];
        return body;
    }

    /// <summary>`phoneSchema.optional().or(z.literal(''))` : la chaîne vide reste vide.</summary>
    public static string? NormalizeOptional(string? phone) => phone is null || phone.Length == 0 ? phone : Normalize(phone);
}
