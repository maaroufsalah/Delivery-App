using System.Security.Cryptography;

namespace Delivery.Domain.Common;

/// <summary>
/// Identifiant au format cuid (v1) : 25 caractères, commence par « c »,
/// comme le `@default(cuid())` de Prisma côté Speedy.
/// </summary>
public static class Cuid
{
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
    private const int BlockSize = 4;
    private static readonly long Base = 1_679_616; // 36^4
    private static readonly string Fingerprint = BuildFingerprint();
    private static int _counter = RandomNumberGenerator.GetInt32(0, (int)Base);

    public static string New()
    {
        var timestamp = ToBase36(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).PadLeft(8, '0');
        var count = Pad(ToBase36(SafeCounter()));
        var random = Pad(ToBase36(RandomNumberGenerator.GetInt32(0, (int)Base)))
                     + Pad(ToBase36(RandomNumberGenerator.GetInt32(0, (int)Base)));
        return "c" + timestamp + count + Fingerprint + random;
    }

    private static int SafeCounter()
    {
        var value = Interlocked.Increment(ref _counter);
        if (value >= Base)
        {
            Interlocked.CompareExchange(ref _counter, 0, value);
            value %= (int)Base;
        }
        return value;
    }

    private static string BuildFingerprint()
    {
        var pid = Pad(ToBase36(Environment.ProcessId)).Substring(2, 2);
        var host = Environment.MachineName;
        var sum = host.Aggregate(host.Length + 36, (acc, ch) => acc + ch);
        return pid + Pad(ToBase36(sum)).Substring(2, 2);
    }

    private static string Pad(string value) =>
        value.Length >= BlockSize ? value[^BlockSize..] : value.PadLeft(BlockSize, '0');

    private static string ToBase36(long value)
    {
        if (value == 0) return "0";
        Span<char> buffer = stackalloc char[16];
        var i = buffer.Length;
        while (value > 0)
        {
            buffer[--i] = Alphabet[(int)(value % 36)];
            value /= 36;
        }
        return new string(buffer[i..]);
    }
}
