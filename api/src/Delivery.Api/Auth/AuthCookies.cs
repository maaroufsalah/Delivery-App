using Delivery.Application.Identity.Dtos;
using Delivery.Domain.Identity;

namespace Delivery.Api.Auth;

/// <summary>Cookies `dp.at` / `dp.rt` : httpOnly, SameSite=Lax, chemin `/` (common/http/cookies.ts).</summary>
public static class AuthCookies
{
    public const string Access = "dp.at";
    public const string Refresh = "dp.rt";

    public static void Write(HttpResponse response, IConfiguration configuration, IssuedSession session)
    {
        Set(response, configuration, Access, session.AccessToken, SessionPolicy.AccessCookieSeconds(session.AccessSeconds));
        Set(response, configuration, Refresh, session.RefreshToken, session.RefreshSeconds);
    }

    public static void Clear(HttpResponse response, IConfiguration configuration)
    {
        foreach (var name in new[] { Access, Refresh })
        {
            response.Cookies.Delete(name, Options(configuration, null));
        }
    }

    public static string? Read(HttpRequest request, string name) =>
        request.Cookies.TryGetValue(name, out var value) && value.Length > 0 ? value : null;

    public static string? Bearer(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        if (!header.StartsWith("bearer ", StringComparison.OrdinalIgnoreCase)) return null;
        var token = header[7..].Trim();
        return token.Length == 0 ? null : token;
    }

    private static void Set(HttpResponse response, IConfiguration configuration, string name, string value, int maxAgeSeconds) =>
        response.Cookies.Append(name, value, Options(configuration, maxAgeSeconds));

    private static CookieOptions Options(IConfiguration configuration, int? maxAgeSeconds)
    {
        var domain = configuration["COOKIE_DOMAIN"];
        var options = new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = string.Equals(configuration["COOKIE_SECURE"], "true", StringComparison.Ordinal),
            Domain = string.IsNullOrEmpty(domain) ? null : domain,
            Path = "/",
        };
        if (maxAgeSeconds is { } seconds)
        {
            options.MaxAge = TimeSpan.FromSeconds(seconds);
            options.Expires = DateTimeOffset.UtcNow.AddSeconds(seconds);
        }
        return options;
    }
}
