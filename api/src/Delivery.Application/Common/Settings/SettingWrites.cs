using System.Text.Json;
using System.Text.Json.Nodes;
using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Common.Settings;

/// <summary>Écriture des paramètres (`AppSettingsService.set`) et accès à la société unique.</summary>
public static class SettingWrites
{
    public static async Task<Company> CompanyAsync(this IAppDbContext db, CancellationToken cancellationToken) =>
        await db.Companies.OrderBy(c => c.CreatedAt).FirstOrDefaultAsync(cancellationToken)
        ?? throw BusinessException.NotFound("Société introuvable.");

    /// <summary>Clé `société:company:clé`, comme l'`upsert` de Speedy.</summary>
    public static async Task SetSettingAsync(this IAppDbContext db, string companyId, string key, JsonNode? value, CancellationToken cancellationToken)
    {
        var id = $"{companyId}:company:{key}";
        var json = value?.ToJsonString() ?? "null";
        var row = await db.Settings.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (row is null)
        {
            db.Settings.Add(new Setting { Id = id, Scope = SettingScope.COMPANY, AgencyId = null, Key = key, Value = json });
        }
        else
        {
            row.Value = json;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>`writeChanged` : n'écrit que les clés dont la valeur change (fallback compris).</summary>
    public static async Task WriteChangedAsync(this IAppDbContext db, string companyId, IReadOnlyList<(string Key, JsonNode? Value, JsonNode? Fallback)> writes, CancellationToken cancellationToken)
    {
        var all = await db.AllSettingsAsync(null, cancellationToken);
        foreach (var (key, value, fallback) in writes)
        {
            var current = all.TryGetValue(key, out var stored) && stored.ValueKind != JsonValueKind.Null ? stored.GetRawText() : fallback?.ToJsonString() ?? "null";
            var next = value?.ToJsonString() ?? "null";
            if (JsonNode.DeepEquals(JsonNode.Parse(current), JsonNode.Parse(next))) continue;
            await db.SetSettingAsync(companyId, key, value, cancellationToken);
        }
    }

    public static async Task<T> GetSettingAsync<T>(this IAppDbContext db, string key, T fallback, CancellationToken cancellationToken)
    {
        var value = await db.SettingAsync(key, cancellationToken);
        if (value is null) return fallback;
        try
        {
            return value.Value.Deserialize<T>() ?? fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }
}
