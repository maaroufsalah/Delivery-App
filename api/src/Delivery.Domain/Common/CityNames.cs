using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Delivery.Domain.Common;

/// <summary>`city-name.ts` et `district-name.ts` : clés de comparaison et noms de ville propres.</summary>
public static class CityNames
{
    private static readonly CompareInfo French = CultureInfo.GetCultureInfo("fr").CompareInfo;

    /// <summary>`localeCompare(b, 'fr')`.</summary>
    public static int CompareFr(string a, string b) => French.Compare(a, b, CompareOptions.None);

    private static string StripMarks(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Normalize(NormalizationForm.FormD))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark) continue;
            builder.Append(ch);
        }
        return builder.ToString();
    }

    /// <summary>`cityKey` : sans accents, minuscules, `[a-z0-9]` seulement.</summary>
    public static string CityKey(string name) =>
        Regex.Replace(StripMarks(name).ToLowerInvariant(), "[^a-z0-9]", string.Empty);

    /// <summary>`districtKey` : comme `cityKey`, lettres non latines conservées.</summary>
    public static string DistrictKey(string name) =>
        Regex.Replace(StripMarks(name).ToLowerInvariant(), @"[^\p{L}\p{N}]", string.Empty);

    /// <summary>`normalizeHeader` des classeurs importés.</summary>
    public static string NormalizeHeader(string value) => CityKey(value);

    private static readonly (string From, string To)[] Spelling =
    [
        ("Fes", "Fès"), ("Meknes", "Meknès"), ("Sale", "Salé"), ("Temara", "Témara"), ("Tetouan", "Tétouan"), ("Tetoun", "Tétouan"),
        ("Kenitra", "Kénitra"), ("Mediouna", "Médiouna"), ("Mouhammedia", "Mohammedia"), ("Beni[- ]Mellal", "Béni Mellal"),
        ("Settate", "Settat"), ("Ouarzazat", "Ouarzazate"), ("Ifran", "Ifrane"), ("Laayoune", "Laâyoune"), ("Ouzzane", "Ouazzane"),
        ("Houcima", "Al Hoceima"), ("Tantan", "Tan-Tan"), ("Tan Tan", "Tan-Tan"), ("Mrirte", "Mrirt"),
        ("Moly Drisse Zarhoune", "Moulay Idriss Zerhoun"), ("Idi Jaber", "Sidi Jaber"), ("Sid L Mokhtar", "Sidi El Mokhtar"),
        ("M Diq", "M’diq"), ("M Gouna", "M’Gouna"), ("Imouzar Kandre", "Imouzzer Kandar"), ("Bouknadl", "Bouknadel"),
        ("Oualmes", "Oulmès"), ("Elhajeb", "El Hajeb"), ("Sidi Aadi", "Sidi Addi"), ("Riche", "Rich"),
    ];

    private static readonly HashSet<string> Particles = ["de", "des", "du"];

    /// <summary>`normalizeCityName` : nom latin propre et nom arabe éventuel.</summary>
    public static (string Name, string? NameAr) Normalize(string raw)
    {
        var value = Regex.Replace(raw.Normalize(NormalizationForm.FormC), "[​-‏‪-‮]", string.Empty).Trim();
        var arabic = Regex.Match(value, "[؀-ۿ][؀-ۿ\\s]*");
        var nameAr = arabic.Success ? arabic.Value.Trim() : null;
        value = new Regex("[\\s-]*[؀-ۿ][؀-ۿ\\s]*").Replace(value, string.Empty, 1);
        value = Regex.Replace(value, @"[\s-]*\bville\b[\s-]*", " ", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"-\d+\s*$", string.Empty);
        value = Regex.Replace(value, @"\s*-\s*", "-");
        value = Regex.Replace(value, @"\s+", " ").Trim();
        var words = value.ToLowerInvariant().Split(' ');
        var name = string.Join(' ', words.Select((word, i) => i > 0 && Particles.Contains(word)
            ? word
            : Regex.Replace(word, @"(^|[-'’])(\p{L})", m => m.Groups[1].Value + m.Groups[2].Value.ToUpperInvariant())));
        name = Regex.Replace(name, @"-Jadida(?!\p{L})", "-El Jadida");
        foreach (var (from, to) in Spelling)
        {
            name = Regex.Replace(name, $@"(?<!\p{{L}}){from}(?!\p{{L}})", to);
        }
        return (name, nameAr);
    }
}
