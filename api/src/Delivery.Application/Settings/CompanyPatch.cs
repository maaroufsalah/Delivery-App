using System.Text.Json;
using System.Text.Json.Nodes;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;

namespace Delivery.Application.Settings;

/// <summary>
/// `companySettingsPatchSchema` (partiel de `companySettingsObject`) lu sur le
/// corps brut : un champ absent garde sa valeur, `null` efface un champ facultatif.
/// </summary>
public static class CompanyPatch
{
    public static readonly string[] Currencies = ["MAD", "EUR", "USD", "AED", "SAR", "TND", "DZD"];
    public static readonly string[] Countries = ["MA", "DZ", "TN", "FR", "ES", "AE", "SA"];
    public static readonly string[] TimeZones = ["Africa/Casablanca", "Africa/Algiers", "Africa/Tunis", "Europe/Paris", "Europe/Madrid", "Asia/Dubai", "Asia/Riyadh"];
    public static readonly string[] Locales = ["fr", "ar", "en"];

    private static readonly (string Key, int Max)[] ProfileTexts =
        [("legalName", 160), ("taxId", 40), ("rc", 60), ("proLegalName", 160), ("proTaxId", 40), ("address", 200), ("city", 80), ("phone", 40)];

    /// <summary>Le patch transformé (chaînes rognées, couleurs telles quelles) ou les problèmes.</summary>
    public static JsonObject Parse(JsonNode? body, List<(string Path, string Message)> issues)
    {
        var output = new JsonObject();
        if (body is not JsonObject input)
        {
            issues.Add((string.Empty, $"Invalid input: expected object, received {(body is null ? "undefined" : "array")}"));
            return output;
        }

        String(input, output, issues, "name", v => v.Length < 2 ? "too-short" : v.Length > 60 ? Zod.TooBigString(60) : null);
        String(input, output, issues, "shortName", v => v.Length < 1 ? "required" : v.Length > CompanySettingsShortMax ? "too-long" : null);
        String(input, output, issues, "logo", v => v.Length < 1 ? "required" : !(v.StartsWith('/') || v.StartsWith("data:image/", StringComparison.Ordinal)) ? "invalid-logo" : null);
        if (input.TryGetPropertyValue("logoBackground", out var background))
        {
            var text = StringOf(background);
            if (background is null) output["logoBackground"] = null;
            else if (text is null || !(text.Length == 0 || Colors.IsHex(text.Trim()))) issues.Add(("logoBackground", Zod.InvalidInput));
            else output["logoBackground"] = text.Length == 0 ? string.Empty : text.Trim();
        }
        String(input, output, issues, "primaryColor", v => Colors.IsHex(v) ? null : "invalid-color");
        String(input, output, issues, "secondaryColor", v => Colors.IsHex(v) ? null : "invalid-color");
        Option(input, output, issues, "currency", Currencies);
        Option(input, output, issues, "country", Countries);
        Option(input, output, issues, "timeZone", TimeZones);
        if (input.TryGetPropertyValue("locales", out var locales))
        {
            if (locales is not JsonArray array) issues.Add(("locales", $"Invalid input: expected array, received {Kind(locales)}"));
            else
            {
                var values = new JsonArray();
                for (var i = 0; i < array.Count; i++)
                {
                    var value = StringOf(array[i]);
                    if (value is null || !Locales.Contains(value)) issues.Add(($"locales.{i}", Zod.InvalidOption(Locales)));
                    else values.Add(value);
                }
                if (array.Count < 1) issues.Add(("locales", "at-least-one-locale"));
                output["locales"] = values;
            }
        }
        Option(input, output, issues, "defaultLocale", Locales);
        foreach (var (key, max) in ProfileTexts)
        {
            if (!input.TryGetPropertyValue(key, out var node)) continue;
            if (node is null)
            {
                output[key] = null;
                continue;
            }
            var text = StringOf(node);
            if (text is null) issues.Add((key, $"Invalid input: expected string, received {Kind(node)}"));
            else if (text.Trim().Length > max) issues.Add((key, Zod.TooBigString(max)));
            else output[key] = text.Trim();
        }
        if (input.TryGetPropertyValue("email", out var email))
        {
            var text = StringOf(email);
            if (email is null) output["email"] = null;
            else if (text is null || !(text.Length == 0 || Zod.IsEmail(text.Trim().ToLowerInvariant()))) issues.Add(("email", Zod.InvalidInput));
            else output["email"] = text.Length == 0 ? string.Empty : text.Trim().ToLowerInvariant();
        }
        if (input.TryGetPropertyValue("website", out var website))
        {
            var text = StringOf(website);
            if (website is null) output["website"] = null;
            else if (text is null || !(text.Length == 0 || Uri.TryCreate(text.Trim(), UriKind.Absolute, out _))) issues.Add(("website", Zod.InvalidInput));
            else output["website"] = text.Length == 0 ? string.Empty : text.Trim();
        }
        if (input.TryGetPropertyValue("theme", out var theme))
        {
            var themeIssues = new List<(string, string)>();
            var parsed = ThemeOverrides.Parse(theme, "theme", themeIssues);
            issues.AddRange(themeIssues);
            output["theme"] = parsed;
        }

        if (issues.Count == 0 && output["locales"] is JsonArray enabled && output["defaultLocale"] is JsonValue chosen
            && !enabled.Any(l => l?.GetValue<string>() == chosen.GetValue<string>()))
        {
            issues.Add(("defaultLocale", "default-locale-not-enabled"));
        }
        return output;
    }

    private const int CompanySettingsShortMax = 20;

    private static void String(JsonObject input, JsonObject output, List<(string, string)> issues, string key, Func<string, string?> check)
    {
        if (!input.TryGetPropertyValue(key, out var node)) return;
        var text = StringOf(node);
        if (text is null)
        {
            issues.Add((key, $"Invalid input: expected string, received {Kind(node)}"));
            return;
        }
        var trimmed = text.Trim();
        var problem = check(trimmed);
        if (problem is not null) issues.Add((key, problem));
        else output[key] = trimmed;
    }

    private static void Option(JsonObject input, JsonObject output, List<(string, string)> issues, string key, string[] values)
    {
        if (!input.TryGetPropertyValue(key, out var node)) return;
        var text = StringOf(node);
        if (text is null || !values.Contains(text)) issues.Add((key, Zod.InvalidOption(values)));
        else output[key] = text;
    }

    public static string? StringOf(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;

    public static string Kind(JsonNode? node) => node switch
    {
        null => "null",
        JsonArray => "array",
        JsonObject => "object",
        JsonValue v => v.GetValueKind() switch
        {
            JsonValueKind.Number => "number",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            _ => "string",
        },
        _ => "undefined",
    };
}
