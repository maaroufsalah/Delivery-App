using Delivery.Application.Common.Interfaces;

namespace Delivery.Application.Settings;

/// <summary>Logos et icônes servis par `/api/public/logo/<fichier>` depuis `logos/<société>/`.</summary>
internal static class Logos
{
    public const string UrlPrefix = "/api/public/logo/";
    public const int MaxBytes = 512 * 1024;

    /// <summary>`logoKeyOf` : la clé de stockage d'une URL de logo de la société, sinon <c>null</c>.</summary>
    public static string? KeyOf(string companyId, string? logoUrl)
    {
        if (logoUrl is null || !logoUrl.StartsWith(UrlPrefix, StringComparison.Ordinal)) return null;
        var fileName = logoUrl[UrlPrefix.Length..];
        if (fileName.Length == 0 || fileName.Contains('/', StringComparison.Ordinal) || fileName.Contains("..", StringComparison.Ordinal)) return null;
        return $"logos/{companyId}/{fileName}";
    }

    public static string UrlOf(StoredFile file) => UrlPrefix + file.Key.Split('/')[^1];

    public static string MimeOf(string fileName) =>
        fileName.EndsWith(".svg", StringComparison.Ordinal) ? "image/svg+xml"
        : fileName.EndsWith(".png", StringComparison.Ordinal) ? "image/png"
        : fileName.EndsWith(".webp", StringComparison.Ordinal) ? "image/webp"
        : "image/jpeg";

    /// <summary>`refreshIcon` : nouvelle icône depuis le logo stocké, ancienne icône effacée.</summary>
    public static async Task<string?> RefreshIconAsync(IFileStorage storage, string companyId, string? logoUrl, string? previousIconUrl, string? background, CancellationToken cancellationToken)
    {
        string? iconUrl = null;
        var logoKey = KeyOf(companyId, logoUrl);
        if (logoKey is not null)
        {
            var source = await storage.GetAsync(logoKey, cancellationToken);
            var icon = await storage.PutAsync($"logos/{companyId}", storage.BrandIcon(source, background), "image/png", cancellationToken);
            iconUrl = UrlOf(icon);
        }
        var oldKey = KeyOf(companyId, previousIconUrl);
        if (oldKey is not null) await storage.RemoveAsync(oldKey, cancellationToken);
        return iconUrl;
    }
}
