using Delivery.Domain.Parcels;

namespace Delivery.Application.Common.Documents;

public static partial class DocumentLabels
{
    public const string DefaultLocale = "fr";

    /// <summary>`toDocumentLocale`.</summary>
    public static string Locale(string? value) => value is "en" or "ar" ? value : DefaultLocale;

    /// <summary>`parcelStatusLabel`.</summary>
    public static string ParcelStatus(int statusId, string locale = DefaultLocale)
    {
        var status = StatusCatalog.Find(statusId);
        if (status is null) return $"#{statusId}";
        return Lookup(ParcelStatusTable, locale, status.Key) ?? status.Key;
    }

    /// <summary>`statusLabel(key, locale)` de `i18n/status-labels.ts` : le libellé d'écran.</summary>
    public static string StatusScreen(int statusId, string locale = DefaultLocale)
    {
        var status = StatusCatalog.Find(statusId);
        if (status is null) return statusId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Lookup(StatusScreenTable, locale, status.Key) ?? status.Key;
    }

    /// <summary>`enumLabel`.</summary>
    public static string Enum(string key, string locale = DefaultLocale) => Lookup(EnumTable, locale, key) ?? key;

    private static string? Lookup(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> table, string locale, string key) =>
        (table.TryGetValue(locale, out var entries) && entries.TryGetValue(key, out var label)) ? label
        : table["fr"].TryGetValue(key, out var fallback) ? fallback
        : null;
}
