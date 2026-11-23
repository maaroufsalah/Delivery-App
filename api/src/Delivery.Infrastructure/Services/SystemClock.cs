using Delivery.Application.Common.Interfaces;

namespace Delivery.Infrastructure.Services;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow
    {
        get
        {
            var now = DateTime.UtcNow;
            return new DateTime(now.Ticks - (now.Ticks % TimeSpan.TicksPerMillisecond), DateTimeKind.Utc);
        }
    }
}
