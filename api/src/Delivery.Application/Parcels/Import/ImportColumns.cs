using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Delivery.Application.Parcels.Import;

/// <summary>`parcel-import.columns.ts` : en-têtes par langue, alias, champs obligatoires, aide.</summary>
public static partial class ImportColumns
{
    public sealed record AnomalySheetLabels(string Name, IReadOnlyList<string> Headers, string Error, string Warning);

    public sealed record HelpSheet(string Sheet, string Name, string Title, IReadOnlyList<(string Label, string Text)> Rows);

    private static readonly Lazy<Tables> Data = new(Load);

    private sealed record Tables(
        IReadOnlyList<string> Fields,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Headers,
        IReadOnlyDictionary<string, IReadOnlyList<string>> Aliases,
        IReadOnlyList<string> Required,
        IReadOnlyDictionary<string, AnomalySheetLabels> Anomalies,
        IReadOnlyDictionary<string, HelpSheet> Help,
        IReadOnlyList<string> TrueWords,
        IReadOnlyList<string> FalseWords);

    public static IReadOnlyList<string> Fields => Data.Value.Fields;

    public static IReadOnlyList<string> Required => Data.Value.Required;

    public static IReadOnlyList<string> TrueWords => Data.Value.TrueWords;

    public static IReadOnlyList<string> FalseWords => Data.Value.FalseWords;

    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Headers => Data.Value.Headers;

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Aliases => Data.Value.Aliases;

    public static string Locale(string? locale) => locale is not null && Data.Value.Headers.ContainsKey(locale) ? locale : "fr";

    public static AnomalySheetLabels AnomalySheet(string locale) => Data.Value.Anomalies.GetValueOrDefault(locale) ?? Data.Value.Anomalies["fr"];

    public static HelpSheet Help(string locale) => Data.Value.Help.GetValueOrDefault(locale) ?? Data.Value.Help["fr"];

    /// <summary>`normalizeLabel` : sans accents, en minuscules, sans espaces ni ponctuation.</summary>
    public static string NormalizeLabel(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (ch is >= '̀' and <= 'ͯ') continue;
            var lower = char.ToLowerInvariant(ch);
            if (char.IsWhiteSpace(lower) || "_-./()[]:;,'\"\\".Contains(lower)) continue;
            builder.Append(lower);
        }
        return builder.ToString();
    }

    private static Tables Load()
    {
        using var document = JsonDocument.Parse(Json);
        var root = document.RootElement;
        static IReadOnlyList<string> Strings(JsonElement element) => element.EnumerateArray().Select(e => e.GetString()!).ToList();
        var headers = root.GetProperty("headers").EnumerateObject().ToDictionary(
            l => l.Name, l => (IReadOnlyDictionary<string, string>)l.Value.EnumerateObject().ToDictionary(f => f.Name, f => f.Value.GetString()!));
        var aliases = root.GetProperty("aliases").EnumerateObject().ToDictionary(f => f.Name, f => Strings(f.Value));
        var anomalies = root.GetProperty("anomalySheet").EnumerateObject().ToDictionary(l => l.Name, l => new AnomalySheetLabels(
            l.Value.GetProperty("name").GetString()!, Strings(l.Value.GetProperty("headers")), l.Value.GetProperty("error").GetString()!, l.Value.GetProperty("warning").GetString()!));
        var help = root.GetProperty("help").EnumerateObject().ToDictionary(l => l.Name, l =>
        {
            var body = l.Value.GetProperty("help");
            var rows = body.GetProperty("rows").EnumerateArray().Select(r => (r[0].GetString()!, r[1].GetString()!)).ToList();
            return new HelpSheet(l.Value.GetProperty("sheet").GetString()!, body.GetProperty("name").GetString()!, body.GetProperty("title").GetString()!, rows);
        });
        return new Tables(Strings(root.GetProperty("fields")), headers, aliases, Strings(root.GetProperty("required")), anomalies, help,
            Strings(root.GetProperty("trueWords")), Strings(root.GetProperty("falseWords")));
    }

    /// <summary>`readAmount` : « 1 250,5 » → 125050 centimes ; vide → 0 ; illisible → null.</summary>
    public static long? ReadAmount(string raw)
    {
        var value = new string(raw.Trim().Where(c => !char.IsWhiteSpace(c)).ToArray());
        var comma = value.IndexOf(',', StringComparison.Ordinal);
        if (comma >= 0) value = string.Concat(value.AsSpan(0, comma), ".", value.AsSpan(comma + 1));
        if (value.Length == 0) return 0;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount) || !double.IsFinite(amount)) return null;
        return (long)Math.Floor(amount * 100 + 0.5);
    }

    /// <summary>`readBoolean` : O/N, oui/non, نعم/لا…</summary>
    public static bool? ReadBoolean(string raw, bool? fallback)
    {
        var value = raw.Trim().ToLowerInvariant();
        if (value.Length == 0) return fallback;
        if (TrueWords.Contains(value)) return true;
        if (FalseWords.Contains(value)) return false;
        return null;
    }
}
