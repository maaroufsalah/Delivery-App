using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Delivery.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Delivery.Infrastructure.Security;

/// <summary>
/// JWT HS256 écrits à la main : la charge utile sort octet pour octet comme
/// celle de `@nestjs/jwt` (mêmes noms courts, `null` conservés), sans le
/// renommage de claims des bibliothèques .NET.
/// </summary>
public sealed class JwtService(IConfiguration configuration, IClock clock) : IJwtService
{
    private static readonly byte[] Header = Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");

    public string Sign(JwtKind kind, IReadOnlyDictionary<string, object?> payload)
    {
        var head = Base64Url(Header);
        var body = Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signature = Base64Url(HMACSHA256.HashData(Secret(kind), Encoding.ASCII.GetBytes($"{head}.{body}")));
        return $"{head}.{body}.{signature}";
    }

    public JwtClaims? Verify(JwtKind kind, string token, bool ignoreExpiration = false)
    {
        var parts = token.Split('.');
        if (parts.Length != 3) return null;
        try
        {
            var expected = HMACSHA256.HashData(Secret(kind), Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"));
            if (!CryptographicOperations.FixedTimeEquals(expected, FromBase64Url(parts[2]))) return null;
            using var header = JsonDocument.Parse(FromBase64Url(parts[0]));
            if (!header.RootElement.TryGetProperty("alg", out var alg) || alg.GetString() != "HS256") return null;
            using var document = JsonDocument.Parse(FromBase64Url(parts[1]));
            var root = document.RootElement;
            var exp = Number(root, "exp");
            var now = new DateTimeOffset(clock.UtcNow).ToUnixTimeSeconds();
            if (!ignoreExpiration && exp is not null && now >= exp) return null;
            var nbf = Number(root, "nbf");
            if (nbf is not null && now < nbf) return null;
            var sub = Text(root, "sub");
            if (sub is null) return null;
            return new JwtClaims(
                sub,
                Text(root, "cid"),
                Text(root, "email") ?? string.Empty,
                root.TryGetProperty("roles", out var roles) && roles.ValueKind == JsonValueKind.Array
                    ? roles.EnumerateArray().Select(r => r.GetString() ?? string.Empty).ToList()
                    : [],
                Text(root, "aid"),
                Text(root, "clid"),
                Text(root, "crid"),
                Bool(root, "mcp"),
                Bool(root, "imp"),
                Text(root, "skd"),
                Text(root, "jti"),
                Text(root, "purpose"),
                Number(root, "iat"),
                exp);
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private byte[] Secret(JwtKind kind)
    {
        var name = kind == JwtKind.Access ? "JWT_ACCESS_SECRET" : "JWT_REFRESH_SECRET";
        var value = configuration[name];
        if (string.IsNullOrEmpty(value) || value.Length < 32)
        {
            throw new InvalidOperationException($"{name} : 32 caractères minimum");
        }
        return Encoding.UTF8.GetBytes(value);
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool Bool(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static long? Number(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? (long)Math.Floor(value.GetDouble()) : null;

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '='));
    }
}
