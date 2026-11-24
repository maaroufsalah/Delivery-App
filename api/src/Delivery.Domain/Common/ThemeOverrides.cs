using System.Text.Json;
using System.Text.Json.Nodes;

namespace Delivery.Domain.Common;

/// <summary>
/// `theme-overrides.ts` : surcharges de couleurs par mode (light/dark) et par
/// section. Validation zod (couleur HEX, contraste AA), normalisation `#AABBCC`,
/// et `pruneTheme` (sections et valeurs vides retirées).
/// </summary>
public static class ThemeOverrides
{
    public static readonly string[] Modes = ["light", "dark"];

    public static readonly IReadOnlyDictionary<string, string[]> Sections = new Dictionary<string, string[]>
    {
        ["sidebar"] = ["background", "foreground", "accent", "border"],
        ["header"] = ["background", "foreground", "border"],
        ["table"] = ["headerBackground", "headerForeground", "rowHover", "border"],
        ["button"] = ["background", "foreground"],
        ["badge"] = ["background", "foreground"],
    };

    private static readonly Dictionary<string, (string Background, string Foreground)> ContrastPairs = new()
    {
        ["sidebar"] = ("background", "foreground"),
        ["header"] = ("background", "foreground"),
        ["table"] = ("headerBackground", "headerForeground"),
        ["button"] = ("background", "foreground"),
        ["badge"] = ("background", "foreground"),
    };

    /// <summary>
    /// `themeOverridesSchema.parse` : rend le thème transformé (clés inconnues retirées,
    /// `''` → absent, HEX normalisé) ou les problèmes `(chemin, message)`.
    /// </summary>
    public static JsonObject Parse(JsonNode? value, string path, List<(string Path, string Message)> issues)
    {
        var result = new JsonObject();
        if (value is not JsonObject root)
        {
            issues.Add((path, $"Invalid input: expected object, received {Kind(value)}"));
            return result;
        }
        foreach (var mode in Modes)
        {
            if (!root.TryGetPropertyValue(mode, out var modeNode) || modeNode is null && !root.ContainsKey(mode)) continue;
            if (modeNode is null)
            {
                issues.Add(($"{path}.{mode}", "Invalid input: expected object, received null"));
                continue;
            }
            if (modeNode is not JsonObject sections)
            {
                issues.Add(($"{path}.{mode}", $"Invalid input: expected object, received {Kind(modeNode)}"));
                continue;
            }
            var parsedSections = new JsonObject();
            foreach (var (name, fields) in Sections)
            {
                if (!sections.TryGetPropertyValue(name, out var sectionNode)) continue;
                var sectionPath = $"{path}.{mode}.{name}";
                if (sectionNode is not JsonObject section)
                {
                    issues.Add((sectionPath, $"Invalid input: expected object, received {Kind(sectionNode)}"));
                    continue;
                }
                var before = issues.Count;
                var parsed = new JsonObject();
                foreach (var field in fields)
                {
                    if (!section.TryGetPropertyValue(field, out var colorNode)) continue;
                    if (colorNode is not JsonValue v || !v.TryGetValue<string>(out var text))
                    {
                        issues.Add(($"{sectionPath}.{field}", "Invalid input"));
                        continue;
                    }
                    if (text.Length == 0) continue;
                    var trimmed = text.Trim();
                    if (!Colors.IsHex(trimmed))
                    {
                        issues.Add(($"{sectionPath}.{field}", "Invalid input"));
                        continue;
                    }
                    parsed[field] = Colors.NormalizeHex(trimmed, "#000000");
                }
                if (issues.Count == before)
                {
                    var (bg, fg) = ContrastPairs[name];
                    var background = parsed[bg]?.GetValue<string>();
                    var foreground = parsed[fg]?.GetValue<string>();
                    if (background is not null && foreground is not null && Colors.ContrastRatio(background, foreground) < Colors.MinContrast)
                    {
                        issues.Add(($"{sectionPath}.{fg}", "contrast-too-low"));
                    }
                }
                parsedSections[name] = parsed;
            }
            result[mode] = parsedSections;
        }
        return result;
    }

    /// <summary>`pruneTheme` : rien que les sections qui portent au moins une couleur.</summary>
    public static JsonObject Prune(JsonNode? theme)
    {
        var result = new JsonObject();
        if (theme is not JsonObject root) return result;
        foreach (var mode in Modes)
        {
            if (root[mode] is not JsonObject sections) continue;
            var kept = new JsonObject();
            foreach (var name in Sections.Keys)
            {
                if (sections[name] is not JsonObject section) continue;
                var entries = new JsonObject();
                foreach (var (key, val) in section)
                {
                    if (val is JsonValue v && v.TryGetValue<string>(out var text) && text.Length > 0) entries[key] = text;
                }
                if (entries.Count > 0) kept[name] = entries;
            }
            if (kept.Count > 0) result[mode] = kept;
        }
        return result;
    }

    public static JsonObject Prune(string? json) => Prune(string.IsNullOrEmpty(json) ? null : JsonNode.Parse(json));

    private static string Kind(JsonNode? node) => node switch
    {
        null => "null",
        JsonArray => "array",
        JsonObject => "object",
        JsonValue v => v.GetValueKind() switch
        {
            JsonValueKind.String => "string",
            JsonValueKind.Number => "number",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            _ => "undefined",
        },
        _ => "undefined",
    };
}
