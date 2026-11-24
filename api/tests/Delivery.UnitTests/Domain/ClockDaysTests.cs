using Delivery.Application.Common.Interfaces;

namespace Delivery.UnitTests.Domain;

public sealed class ClockDaysTests
{
    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    [Fact]
    public void Today_is_the_casablanca_day_not_the_utc_day()
    {
        // 23 h 30 UTC le 15 juin = 0 h 30 le 16 à Casablanca (UTC+1). Un jour de juin : les données
        // Windows du fuseau marocain sont fausses d'octobre à mars 2026 (UTC+0), tzdata Linux est juste.
        var clock = new FixedClock(new DateTime(2026, 6, 15, 23, 30, 0, DateTimeKind.Utc));
        clock.Today().ShouldBe("2026-06-16");
        clock.TodayStartUtc().ShouldBe(new DateTime(2026, 6, 15, 23, 0, 0, DateTimeKind.Utc));
        clock.TomorrowStartUtc().ShouldBe(new DateTime(2026, 6, 16, 23, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Morning_utc_stays_on_the_same_local_day()
    {
        var clock = new FixedClock(new DateTime(2026, 11, 24, 8, 0, 0, DateTimeKind.Utc));
        clock.Today().ShouldBe("2026-11-24");
    }
}
