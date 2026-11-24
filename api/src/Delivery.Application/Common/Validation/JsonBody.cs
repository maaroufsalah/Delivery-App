using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Delivery.Application.Common.Validation;

/// <summary>
/// Lecture d'un corps JSON brut à la manière d'un schéma zod `.partial()` :
/// on sait si une clé est présente, absente ou `null`, et chaque problème est
/// noté avec le message zod attendu.
/// </summary>
public sealed class JsonBody(JsonNode? root)
{
    public JsonObject Object { get; } = root as JsonObject ?? [];

    public bool IsObject { get; } = root is JsonObject;

    public List<(string Path, string Message)> Issues { get; } = [];

    public bool Has(string key) => Object.ContainsKey(key);

    public bool IsNull(string key) => Object.TryGetPropertyValue(key, out var node) && node is null;

    /// <summary>Chaîne rognée ; <c>null</c> si absente ou nulle (le problème éventuel est noté).</summary>
    public string? Text(string key, int? min = null, int? max = null, string? minMessage = null, string? maxMessage = null, bool nullable = false, bool trim = true)
    {
        if (!Object.TryGetPropertyValue(key, out var node)) return null;
        if (node is null)
        {
            if (!nullable) Issues.Add((key, Zod.Expected("string", "null")));
            return null;
        }
        if (node is not JsonValue value || value.GetValueKind() != JsonValueKind.String)
        {
            Issues.Add((key, Zod.Expected("string", Kind(node))));
            return null;
        }
        var text = value.GetValue<string>();
        if (trim) text = text.Trim();
        if (min is not null && text.Length < min) Issues.Add((key, minMessage ?? Zod.TooSmallString(min.Value)));
        else if (max is not null && text.Length > max) Issues.Add((key, maxMessage ?? Zod.TooBigString(max.Value)));
        return text;
    }

    /// <summary>`schema.optional().or(z.literal(''))` : chaîne vide admise, sinon la règle ; échec → « Invalid input ».</summary>
    public string? TextOrEmpty(string key, Func<string, bool> valid, bool nullable = false)
    {
        if (!Object.TryGetPropertyValue(key, out var node)) return null;
        if (node is null)
        {
            if (!nullable) Issues.Add((key, Zod.InvalidInput));
            return null;
        }
        var text = node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
        if (text is null || !(text.Length == 0 || valid(text)))
        {
            Issues.Add((key, Zod.InvalidInput));
            return null;
        }
        return text;
    }

    public string? Option(string key, IReadOnlyCollection<string> values)
    {
        if (!Object.TryGetPropertyValue(key, out var node)) return null;
        var text = node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
        if (text is null || !values.Contains(text))
        {
            Issues.Add((key, Zod.InvalidOption(values)));
            return null;
        }
        return text;
    }

    public bool? Bool(string key)
    {
        if (!Object.TryGetPropertyValue(key, out var node)) return null;
        if (node is JsonValue value && value.GetValueKind() is JsonValueKind.True or JsonValueKind.False) return value.GetValue<bool>();
        Issues.Add((key, Zod.Expected("boolean", Kind(node))));
        return null;
    }

    public static bool Matches(Regex pattern, string value) => pattern.IsMatch(value);

    public static string Kind(JsonNode? node) => node switch
    {
        null => "null",
        JsonArray => "array",
        JsonObject => "object",
        JsonValue v => v.GetValueKind() switch
        {
            JsonValueKind.Number => "number",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.String => "string",
            _ => "undefined",
        },
        _ => "undefined",
    };
}
