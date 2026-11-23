namespace Delivery.Domain.Identity;

/// <summary>Durées et genres de session (`@delivery/shared/session.ts`).</summary>
public static class SessionPolicy
{
    public const string Web = "web";
    public const string Remember = "remember";
    public const string Mobile = "mobile";

    public const int Minute = 60;
    public const int Day = 24 * 60 * 60;
    public const int RenewalMarginSeconds = 60;

    /// <summary>`REFRESH_REUSE_GRACE_MS` : fenêtre où un refresh rejoué reçoit le même enfant.</summary>
    public static readonly TimeSpan ReuseGrace = TimeSpan.FromSeconds(10);

    public static readonly SessionDurations Defaults = new(15 * Minute, 7 * Day, 30 * Day, 30 * Day);

    public static string KindOf(string? value) => value is Web or Remember or Mobile ? value : Web;

    public static int AccessCookieSeconds(int accessSeconds) => Math.Max(30, accessSeconds - RenewalMarginSeconds);

    public static int RefreshSecondsFor(string kind, SessionDurations durations) => kind switch
    {
        Mobile => durations.MobileSeconds,
        Remember => durations.RememberSeconds,
        _ => durations.WebSeconds,
    };

    /// <summary>`sessionDurationsFrom` : minutes/jours des paramètres, convertis et bornés.</summary>
    public static SessionDurations From(double? accessMinutes, double? webDays, double? rememberDays, double? mobileDays) => new(
        Clamp(accessMinutes * Minute, Defaults.AccessSeconds, 5 * Minute, 60 * Minute),
        Clamp(webDays * Day, Defaults.WebSeconds, 1 * Day, 90 * Day),
        Clamp(rememberDays * Day, Defaults.RememberSeconds, 1 * Day, 90 * Day),
        Clamp(mobileDays * Day, Defaults.MobileSeconds, 1 * Day, 90 * Day));

    private static int Clamp(double? value, int fallback, int min, int max)
    {
        if (value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value)) return fallback;
        // Math.round de JS : demi vers +∞.
        var rounded = (int)Math.Floor(value.Value + 0.5);
        return Math.Min(max, Math.Max(min, rounded));
    }
}

public sealed record SessionDurations(int AccessSeconds, int WebSeconds, int RememberSeconds, int MobileSeconds);
