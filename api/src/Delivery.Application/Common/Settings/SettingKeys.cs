using System.Text.Json;
using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Enums;
using Delivery.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Common.Settings;

/// <summary>Clés de `SETTING_KEYS` (common/settings/app-settings.service.ts).</summary>
public static class SettingKeys
{
    public const string FailureFeeSameCity = "pricing.failureFee.sameCity";
    public const string FailureFeeCrossCity = "pricing.failureFee.crossCity";
    public const string PickupFee = "pricing.pickupFee";
    public const string FreePickupThreshold = "pricing.freePickupThreshold";
    public const string ParcelCodePrefix = "parcels.codePrefix";
    public const string RegistrationMerchant = "registration.merchant";
    public const string RegistrationCourier = "registration.courier";
    public const string SessionAccessMinutes = "session.accessMinutes";
    public const string SessionWebDays = "session.webDays";
    public const string SessionRememberDays = "session.rememberDays";
    public const string SessionMobileDays = "session.mobileDays";
}

/// <summary>Lecture des paramètres société, surchargés par l'agence (`AppSettingsService.all`).</summary>
public static class SettingsQueries
{
    public static async Task<Dictionary<string, JsonElement>> AllSettingsAsync(this IAppDbContext db, string? agencyId, CancellationToken cancellationToken)
    {
        var rows = await db.Settings.AsNoTracking()
            .Where(s => s.Scope == SettingScope.COMPANY || (agencyId != null && s.Scope == SettingScope.AGENCY && s.AgencyId == agencyId))
            .Select(s => new { s.Scope, s.Key, s.Value })
            .ToListAsync(cancellationToken);
        var resolved = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var row in rows.Where(r => r.Scope == SettingScope.COMPANY).Concat(rows.Where(r => r.Scope == SettingScope.AGENCY)))
        {
            resolved[row.Key] = JsonDocument.Parse(row.Value).RootElement.Clone();
        }
        return resolved;
    }

    public static async Task<JsonElement?> SettingAsync(this IAppDbContext db, string key, CancellationToken cancellationToken, string? agencyId = null)
    {
        var all = await db.AllSettingsAsync(agencyId, cancellationToken);
        return all.TryGetValue(key, out var value) && value.ValueKind != JsonValueKind.Null ? value : null;
    }

    public static async Task<bool> BoolSettingAsync(this IAppDbContext db, string key, bool fallback, CancellationToken cancellationToken)
    {
        var value = await db.SettingAsync(key, cancellationToken);
        return value?.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            null => fallback,
            // Speedy rend la valeur telle quelle (`value as T`) : une valeur non booléenne est « vraie ».
            _ => true,
        };
    }

    public static async Task<SessionDurations> SessionDurationsAsync(this IAppDbContext db, CancellationToken cancellationToken)
    {
        var all = await db.AllSettingsAsync(null, cancellationToken);
        return SessionPolicy.From(
            NumberOf(all, SettingKeys.SessionAccessMinutes),
            NumberOf(all, SettingKeys.SessionWebDays),
            NumberOf(all, SettingKeys.SessionRememberDays),
            NumberOf(all, SettingKeys.SessionMobileDays));
    }

    private static double? NumberOf(Dictionary<string, JsonElement> all, string key) =>
        all.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
}
