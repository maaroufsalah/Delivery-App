namespace Delivery.Application.Common.Models;

/// <summary>Un fichier rendu par l'API (`StreamableFile`).</summary>
public sealed record FileContent(byte[] Bytes, string ContentType, string FileName, bool Inline = false, string? CacheControl = null)
{
    public const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const string Pdf = "application/pdf";
}
