using System.Globalization;

namespace Delivery.Domain.Common;

/// <summary>Une fenêtre de dates locales (`ResolvedWindow`) et ses bornes UTC.</summary>
public sealed record PeriodWindow(string Period, string From, string To, string Granularity, DateTime Start, DateTime End);

/// <summary>`common/dashboard/period.ts` et `dashboard-period.ts` : périodes des tableaux de bord et des filtres.</summary>
public static class Periods
{
    public static readonly string[] All = ["today", "yesterday", "week", "last7", "last30", "month", "prevMonth", "year", "all", "custom"];

    public const string DefaultTimeZone = "Africa/Casablanca";

    public static bool IsPeriod(string? value) => value is not null && All.Contains(value);

    public static TimeZoneInfo Zone(string? timeZone)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZone ?? DefaultTimeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(DefaultTimeZone);
        }
    }

    /// <summary>`localDay` : la date (AAAA-MM-JJ) d'un instant dans le fuseau.</summary>
    public static string LocalDay(DateTime utc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>`zonedDayStart` : minuit local de ce jour, en UTC.</summary>
    public static DateTime ZonedDayStart(string day, TimeZoneInfo zone)
    {
        var naive = DateTime.SpecifyKind(DateTime.ParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture), DateTimeKind.Utc);
        var offset = zone.GetUtcOffset(naive);
        return naive - offset;
    }

    public static string AddDays(string day, int n) =>
        DateTime.ParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture).AddDays(n).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string MondayOf(string day)
    {
        var date = DateTime.ParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var isoWeekday = ((int)date.DayOfWeek + 6) % 7;
        return AddDays(day, -isoWeekday);
    }

    public static string FirstOfMonth(string day) => day[..7] + "-01";

    public static int SpanDays(string from, string to) =>
        (int)Math.Round((DateTime.ParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture) - DateTime.ParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture)).TotalDays) + 1;

    public static string GranularityForSpan(int spanDays) => spanDays <= 31 ? "day" : spanDays <= 182 ? "week" : "month";

    public static PeriodWindow Resolve(string period, string? from, string? to, TimeZoneInfo zone, DateTime? firstDataAt, DateTime now)
    {
        var today = LocalDay(now, zone);
        string start;
        string end;
        switch (period)
        {
            case "today":
                start = end = today;
                break;
            case "yesterday":
                start = end = AddDays(today, -1);
                break;
            case "week":
                start = MondayOf(today);
                end = today;
                break;
            case "last7":
                start = AddDays(today, -6);
                end = today;
                break;
            case "last30":
                start = AddDays(today, -29);
                end = today;
                break;
            case "month":
                start = FirstOfMonth(today);
                end = today;
                break;
            case "prevMonth":
                var first = FirstOfMonth(today);
                start = FirstOfMonth(AddDays(first, -1));
                end = AddDays(first, -1);
                break;
            case "year":
                start = today[..4] + "-01-01";
                end = today;
                break;
            case "all":
                start = firstDataAt is null ? today : LocalDay(firstDataAt.Value, zone);
                end = today;
                break;
            default:
                var a = from ?? today;
                var b = to ?? today;
                (start, end) = string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
                break;
        }
        var granularity = period == "today" ? "day" : GranularityForSpan(SpanDays(start, end));
        return new PeriodWindow(period, start, end, granularity, ZonedDayStart(start, zone), ZonedDayStart(AddDays(end, 1), zone));
    }

    /// <summary>`previousWindow` : la période de comparaison, ou <c>null</c> pour « tout ».</summary>
    public static PeriodWindow? Previous(PeriodWindow window, TimeZoneInfo zone)
    {
        string from;
        string to;
        switch (window.Period)
        {
            case "all":
                return null;
            case "month":
                var prevFirst = FirstOfMonth(AddDays(window.From, -1));
                var prevLast = AddDays(window.From, -1);
                from = prevFirst;
                to = MinDay($"{prevFirst[..7]}-{window.To[8..10]}", prevLast);
                break;
            case "prevMonth":
                from = FirstOfMonth(AddDays(window.From, -1));
                to = AddDays(window.From, -1);
                break;
            case "year":
                var year = int.Parse(window.From[..4], CultureInfo.InvariantCulture) - 1;
                from = $"{year}-01-01";
                to = MinDay($"{year}{window.To[4..]}", $"{year}-12-31");
                if (!IsValidDay(to)) to = $"{year}-02-28";
                break;
            default:
                var span = SpanDays(window.From, window.To);
                from = AddDays(window.From, -span);
                to = AddDays(window.To, -span);
                break;
        }
        return new PeriodWindow(window.Period, from, to, window.Granularity, ZonedDayStart(from, zone), ZonedDayStart(AddDays(to, 1), zone));
    }

    /// <summary>`windowBuckets` : un seau par jour, semaine ou mois.</summary>
    public static List<(string Date, DateTime Start)> Buckets(PeriodWindow window, TimeZoneInfo zone)
    {
        var buckets = new List<(string, DateTime)>();
        var cursor = window.Granularity switch
        {
            "week" => MondayOf(window.From),
            "month" => FirstOfMonth(window.From),
            _ => window.From,
        };
        while (string.CompareOrdinal(cursor, window.To) <= 0)
        {
            buckets.Add((cursor, ZonedDayStart(cursor, zone)));
            cursor = window.Granularity switch
            {
                "week" => AddDays(cursor, 7),
                "month" => ShiftMonths(cursor, 1),
                _ => AddDays(cursor, 1),
            };
        }
        return buckets;
    }

    public static string ShiftMonths(string day, int n)
    {
        var date = DateTime.ParseExact(day[..7] + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture).AddMonths(n);
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static int BucketIndexFor(IReadOnlyList<(string Date, DateTime Start)> buckets, string day)
    {
        for (var i = buckets.Count - 1; i >= 0; i--)
        {
            if (string.CompareOrdinal(buckets[i].Date, day) <= 0) return i;
        }
        return -1;
    }

    private static string MinDay(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? a : b;

    private static bool IsValidDay(string day) => DateTime.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
