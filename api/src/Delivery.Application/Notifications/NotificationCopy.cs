using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Delivery.Application.Notifications;

public sealed record NotificationText(string Title, string Body);

/// <summary>
/// `notification-copy.ts` + `plural.ts` : ce qu'un push DIT, rendu au moment de l'envoi dans la langue de l'appareil
/// (repli sur le français). Le catalogue est la copie exacte de `NOTIFICATION_COPY`.
/// </summary>
public static partial class NotificationCopy
{
    private sealed record Entry(string Title, string Body);

    private static readonly Lazy<Dictionary<string, Dictionary<string, Entry>>> Catalogue = new(Load);

    private static Dictionary<string, Dictionary<string, Entry>> Load()
    {
        using var stream = typeof(NotificationCopy).Assembly.GetManifestResourceStream("Delivery.Application.Notifications.Resources.notification-copy.json")
            ?? throw new InvalidOperationException("Catalogue des notifications introuvable.");
        return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Entry>>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    public static IReadOnlyCollection<string> Topics => Catalogue.Value.Keys;

    /// <summary>`notificationCopy` : null pour un sujet inconnu.</summary>
    public static NotificationText? For(string topic, string locale, IReadOnlyDictionary<string, string>? parameters)
    {
        if (!Catalogue.Value.TryGetValue(topic, out var entry)) return null;
        var language = entry.ContainsKey(locale) ? locale : "fr";
        var copy = entry[language];
        return new NotificationText(Format(copy.Title, parameters, language), Format(copy.Body, parameters, language));
    }

    /// <summary>`pluralCategory` : règles CLDR réduites à l'arabe, au français et à l'anglais.</summary>
    public static string PluralCategory(string locale, double count)
    {
        var n = Math.Abs(Math.Truncate(count));
        var language = locale.Length >= 2 ? locale[..2] : locale;
        if (language == "ar")
        {
            if (n == 0) return "zero";
            if (n == 1) return "one";
            if (n == 2) return "two";
            var mod = n % 100;
            if (mod is >= 3 and <= 10) return "few";
            if (mod is >= 11 and <= 99) return "many";
            return "other";
        }
        if (language == "fr") return n < 2 ? "one" : "other";
        return n == 1 ? "one" : "other";
    }

    private static int Closing(string text, int start)
    {
        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0) return i;
        }
        return -1;
    }

    private static Dictionary<string, string> Branches(string body)
    {
        var found = new Dictionary<string, string>();
        var i = 0;
        while (i < body.Length)
        {
            var open = body.IndexOf('{', i);
            if (open == -1) break;
            var selector = body[i..open].Trim();
            var end = Closing(body, open);
            if (end == -1) break;
            found[selector] = body[(open + 1)..end];
            i = end + 1;
        }
        return found;
    }

    /// <summary>`formatMessage` : `{name}` et `{count, plural, …}` (avec `#`), le reste tel quel.</summary>
    public static string Format(string template, IReadOnlyDictionary<string, string>? values, string locale)
    {
        if (values is null) return template;
        var output = new StringBuilder();
        var i = 0;
        while (i < template.Length)
        {
            var open = template.IndexOf('{', i);
            if (open == -1)
            {
                output.Append(template, i, template.Length - i);
                break;
            }
            output.Append(template, i, open - i);
            var end = Closing(template, open);
            if (end == -1)
            {
                output.Append(template, open, template.Length - open);
                break;
            }
            var inner = template[(open + 1)..end];
            var match = PluralPattern().Match(inner);
            if (match.Success)
            {
                var raw = values.TryGetValue(match.Groups[1].Value, out var v) ? v : "0";
                var count = double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : double.NaN;
                var options = Branches(match.Groups[2].Value);
                var text = Text(count);
                var chosen = options.GetValueOrDefault($"={text}") ?? options.GetValueOrDefault(PluralCategory(locale, double.IsNaN(count) ? 0 : count))
                    ?? options.GetValueOrDefault("other") ?? string.Empty;
                output.Append(Format(chosen.Replace("#", text, StringComparison.Ordinal), values, locale));
            }
            else
            {
                var name = inner.Trim();
                output.Append(values.TryGetValue(name, out var value) ? value : $"{{{inner}}}");
            }
            i = end + 1;
        }
        return output.ToString();
    }

    private static string Text(double count) => double.IsNaN(count) ? "NaN" : count.ToString(CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^\s*(\w+)\s*,\s*plural\s*,([\s\S]*)$")]
    private static partial Regex PluralPattern();
}
