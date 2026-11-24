using System.Text.Json;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;

namespace Delivery.Application.Dashboard;

/// <summary>`dashboard-settings.ts` : les seuils réglables des alertes, société puis agence.</summary>
public static class DashboardThresholds
{
    public static readonly DashboardThresholdsDto Defaults = new(24, 3, 0.3, 0.3, 0.15, 10);

    public static async Task<DashboardThresholdsDto> LoadAsync(IAppDbContext db, string? agencyId, CancellationToken cancellationToken)
    {
        var all = await db.AllSettingsAsync(agencyId, cancellationToken);
        double Read(string key, double fallback, double max = double.PositiveInfinity) => Positive(all.TryGetValue(key, out var value) ? value : null, fallback, max);
        var d = Defaults;
        return new DashboardThresholdsDto(
            Read(SettingKeys.DashboardCodLateHours, d.CodLateHours),
            Read(SettingKeys.DashboardStuckParcelDays, d.StuckParcelDays),
            Read(SettingKeys.DashboardCourierReturnRate, d.CourierReturnRate, 1),
            Read(SettingKeys.DashboardCityFailureRate, d.CityFailureRate, 1),
            Read(SettingKeys.DashboardPerformanceDropPts, d.PerformanceDropPts, 1),
            Read(SettingKeys.DashboardAlertMinParcels, d.MinParcels));
    }

    /// <summary>`positive` : un nombre (ou une chaîne numérique) fini, &gt; 0 et ≤ max, sinon le défaut.</summary>
    private static double Positive(JsonElement? value, double fallback, double max)
    {
        double parsed;
        if (value is { ValueKind: JsonValueKind.Number } number) parsed = number.GetDouble();
        else if (value is { ValueKind: JsonValueKind.String }) parsed = JsCoerce.Number(value);
        else return fallback;
        return double.IsFinite(parsed) && parsed > 0 && parsed <= max ? parsed : fallback;
    }
}
