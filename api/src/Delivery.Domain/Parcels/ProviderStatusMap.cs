using System.Text.Json;

namespace Delivery.Domain.Parcels;

/// <summary>`provider-status-map.ts` : le statut d'un prestataire vers le nôtre (identité par défaut).</summary>
public static class ProviderStatusMap
{
    public static readonly int[] ProviderStatusIds = [3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38];

    /// <summary>`DEFAULT_PROVIDER_STATUS_MAP` + la surcharge en base, si elle est valide.</summary>
    public static IReadOnlyDictionary<int, int?> Effective(string? overrideJson)
    {
        var merged = ProviderStatusIds.ToDictionary(id => id, id => (int?)id);
        if (string.IsNullOrEmpty(overrideJson)) return merged;
        Dictionary<int, int?> parsed = [];
        try
        {
            using var document = JsonDocument.Parse(overrideJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return merged;
            foreach (var entry in document.RootElement.EnumerateObject())
            {
                if (!int.TryParse(entry.Name, out var key) || !ProviderStatusIds.Contains(key)) return merged;
                if (entry.Value.ValueKind == JsonValueKind.Null) parsed[key] = null;
                else if (entry.Value.ValueKind == JsonValueKind.Number && entry.Value.TryGetInt32(out var target) && target > 0 && StatusCatalog.Find(target) is not null) parsed[key] = target;
                else return merged;
            }
        }
        catch (JsonException)
        {
            return merged;
        }
        foreach (var (key, value) in parsed) merged[key] = value;
        return merged;
    }

    /// <summary>`mapProviderStatus`.</summary>
    public static int? Map(IReadOnlyDictionary<int, int?> map, int providerStatus) => map.TryGetValue(providerStatus, out var target) ? target : null;
}
