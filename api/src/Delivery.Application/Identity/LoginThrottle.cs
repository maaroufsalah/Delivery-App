using System.Collections.Concurrent;
using Delivery.Domain.Common;

namespace Delivery.Application.Identity;

/// <summary>
/// Frein des connexions par adresse (`LoginThrottleService`) : 10 échecs par
/// couple adresse + identifiant, 100 par adresse, sur 15 minutes. En mémoire,
/// comme Speedy : un redémarrage remet les compteurs à zéro.
/// </summary>
public sealed class LoginThrottle
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    public const int MaxFailuresPerLogin = 10;
    public const int MaxFailuresPerAddress = 100;
    private const int MaxKeys = 20_000;

    private readonly ConcurrentDictionary<string, Counter> _windows = new(StringComparer.Ordinal);

    private sealed class Counter
    {
        public int Failures;
        public DateTime ResetAt;
    }

    public void AssertAllowed(string address, string login, DateTime now)
    {
        AssertUnder($"ip:{address}", MaxFailuresPerAddress, now);
        AssertUnder($"login:{address}|{Normalized(login)}", MaxFailuresPerLogin, now);
    }

    public void RecordFailure(string address, string login, DateTime now)
    {
        Count($"ip:{address}", now);
        Count($"login:{address}|{Normalized(login)}", now);
    }

    private static string Normalized(string login) => login.Trim().ToLowerInvariant();

    private void AssertUnder(string key, int max, DateTime now)
    {
        if (!_windows.TryGetValue(key, out var window) || now >= window.ResetAt) return;
        if (window.Failures < max) return;
        throw BusinessException.TooManyRequests(
            "Trop de tentatives depuis cet appareil, réessayez dans quelques minutes.",
            new { reason = "login-throttled", retryAfterSeconds = (int)Math.Ceiling((window.ResetAt - now).TotalMilliseconds / 1000) });
    }

    private void Count(string key, DateTime now)
    {
        lock (_windows)
        {
            if (!_windows.TryGetValue(key, out var window) || now >= window.ResetAt)
            {
                EvictIfNeeded(now);
                _windows[key] = new Counter { Failures = 1, ResetAt = now + Window };
                return;
            }
            window.Failures += 1;
        }
    }

    private void EvictIfNeeded(DateTime now)
    {
        if (_windows.Count < MaxKeys) return;
        foreach (var (key, window) in _windows)
        {
            if (now >= window.ResetAt) _windows.TryRemove(key, out _);
        }
        if (_windows.Count >= MaxKeys) _windows.Clear();
    }
}
