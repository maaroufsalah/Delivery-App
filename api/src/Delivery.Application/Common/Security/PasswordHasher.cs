using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Delivery.Application.Common.Security;

/// <summary>
/// Argon2id au format PHC, compatible avec `@node-rs/argon2` (paramètres par
/// défaut : m=19456, t=2, p=1, sel 16 octets, empreinte 32 octets) : les comptes
/// de Speedy se reconnectent sans réinitialisation. Les comptes repris de Wimo Laravel gardent leur
/// empreinte bcrypt (`$2y$`, `password_hash` de PHP) : vérifiée telle quelle, puis remplacée par un
/// argon2id à la première connexion réussie (<see cref="NeedsRehash"/>).
/// </summary>
public static class PasswordHasher
{
    private const int MemoryKb = 19456;
    private const int Iterations = 2;
    private const int Parallelism = 1;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Compute(password, salt, MemoryKb, Iterations, Parallelism, HashBytes);
        return $"$argon2id$v=19$m={MemoryKb},t={Iterations},p={Parallelism}${B64(salt)}${B64(hash)}";
    }

    /// <summary>Une empreinte héritée (bcrypt) : à refaire en argon2id dès que le mot de passe est connu.</summary>
    public static bool NeedsRehash(string encoded) => !encoded.StartsWith("$argon2id$", StringComparison.Ordinal);

    public static bool Verify(string encoded, string password)
    {
        if (IsBcrypt(encoded)) return VerifyBcrypt(encoded, password);
        try
        {
            var parts = encoded.Split('$');
            if (parts.Length != 6 || parts[1] != "argon2id") return false;
            var settings = parts[3].Split(',').Select(p => p.Split('=')).ToDictionary(p => p[0], p => int.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture));
            var salt = FromB64(parts[4]);
            var expected = FromB64(parts[5]);
            var actual = Compute(password, salt, settings["m"], settings["t"], settings["p"], expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (Exception exception) when (exception is FormatException or KeyNotFoundException or IndexOutOfRangeException or ArgumentException)
        {
            return false;
        }
    }

    private static bool IsBcrypt(string encoded) =>
        encoded.Length == 60 && (encoded.StartsWith("$2y$", StringComparison.Ordinal) || encoded.StartsWith("$2a$", StringComparison.Ordinal)
                                 || encoded.StartsWith("$2b$", StringComparison.Ordinal));

    private static bool VerifyBcrypt(string encoded, string password)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, encoded);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }

    private static byte[] Compute(string password, byte[] salt, int memoryKb, int iterations, int parallelism, int length)
    {
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memoryKb,
            Iterations = iterations,
            DegreeOfParallelism = parallelism,
        };
        return argon.GetBytes(length);
    }

    private static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=');

    private static byte[] FromB64(string value)
    {
        var padded = value.PadRight(value.Length + ((4 - (value.Length % 4)) % 4), '=');
        return Convert.FromBase64String(padded);
    }
}
