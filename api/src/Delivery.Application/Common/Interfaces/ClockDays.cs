using Delivery.Domain.Common;

namespace Delivery.Application.Common.Interfaces;

/// <summary>
/// Les jours « du jour » de l'application : toujours dans le fuseau de la société (Africa/Casablanca),
/// jamais la date UTC brute (un circuit créé à 00 h 30 heure locale appartient au jour local).
/// </summary>
public static class ClockDays
{
    public static TimeZoneInfo Zone => Periods.Zone(Periods.DefaultTimeZone);

    /// <summary>La date locale du jour (AAAA-MM-JJ).</summary>
    public static string Today(this IClock clock) => Periods.LocalDay(clock.UtcNow, Zone);

    /// <summary>Minuit local du jour, en UTC.</summary>
    public static DateTime TodayStartUtc(this IClock clock) => Periods.ZonedDayStart(clock.Today(), Zone);

    /// <summary>Minuit local du lendemain, en UTC (borne exclusive du jour).</summary>
    public static DateTime TomorrowStartUtc(this IClock clock) => Periods.ZonedDayStart(Periods.AddDays(clock.Today(), 1), Zone);
}
