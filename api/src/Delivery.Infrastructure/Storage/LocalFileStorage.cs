using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Microsoft.Extensions.Configuration;
using SkiaSharp;

namespace Delivery.Infrastructure.Storage;

/// <summary>
/// Volume local (`LocalStorageProvider` de Speedy) : `UPLOADS_DIR/dossier/uuid.ext`.
/// Les images passent par SkiaSharp comme elles passaient par sharp.
/// </summary>
public sealed class LocalFileStorage(IConfiguration configuration) : IFileStorage
{
    private static readonly Dictionary<string, string> Extensions = new(StringComparer.Ordinal)
    {
        ["application/pdf"] = "pdf",
        ["application/zip"] = "zip",
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp",
        ["image/svg+xml"] = "svg",
    };

    private static readonly Dictionary<string, string> Family = new(StringComparer.Ordinal)
    {
        ["image/png"] = "png",
        ["image/jpeg"] = "jpeg",
        ["image/jpg"] = "jpeg",
        ["image/webp"] = "webp",
        ["image/svg+xml"] = "svg",
    };

    private const long MaxInputPixels = 50_000_000;
    private const int MaxInputSide = 12_000;
    private const float SvgScale = 2f; // `density: 192` = 2 × 96 dpi.

    private string Root => Path.GetFullPath(configuration["UPLOADS_DIR"] ?? "./var/uploads");

    public async Task<StoredFile> PutAsync(string folder, byte[] content, string mimeType, CancellationToken cancellationToken)
    {
        var extension = Extensions.GetValueOrDefault(mimeType, "bin");
        var key = $"{SafeFolder(folder)}/{Guid.NewGuid()}.{extension}";
        var target = PathOf(key);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await File.WriteAllBytesAsync(target, content, cancellationToken);
        return new StoredFile(key, content.LongLength, mimeType, Convert.ToHexStringLower(SHA256.HashData(content)));
    }

    public async Task<byte[]> GetAsync(string key, CancellationToken cancellationToken)
    {
        var target = PathOf(key);
        if (!File.Exists(target)) throw BusinessException.NotFound("Fichier introuvable.");
        return await File.ReadAllBytesAsync(target, cancellationToken);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        var target = PathOf(key);
        if (File.Exists(target)) File.Delete(target);
        return Task.CompletedTask;
    }

    public SanitizedImage SanitizeImage(byte[] input, string declaredMime, int maxSide, bool preserveFormat = false)
    {
        if (input.Length == 0) throw Rejected("image-empty", "Le fichier est vide.");
        if (!Family.TryGetValue(declaredMime.ToLowerInvariant(), out var allowed))
        {
            throw Rejected("image-unsupported", $"Type non pris en charge : {declaredMime}.");
        }
        var format = Sniff(input);
        if (format is null) throw Rejected("image-undecodable", "Le fichier n’est pas une image lisible.");
        if (format != allowed) throw Rejected("image-type-mismatch", $"Le contenu ({format}) ne correspond pas au type annoncé ({declaredMime}).");

        using var source = format == "svg" ? RenderSvg(input) : DecodeOriented(input);
        if (source is null) throw Rejected("image-undecodable", "Le fichier n’est pas une image lisible.");
        if (source.Width > MaxInputSide || source.Height > MaxInputSide || (long)source.Width * source.Height > MaxInputPixels)
        {
            throw Rejected("image-too-large", "L’image est trop grande.");
        }

        var scale = Math.Min(1d, Math.Min((double)maxSide / source.Width, (double)maxSide / source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        using var resized = scale < 1 ? source.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKCubicResampler.Mitchell)) : source.Copy();
        var target = preserveFormat && format is "jpeg" or "webp" ? format : "png";
        using var image = SKImage.FromBitmap(resized);
        using var data = target switch
        {
            "jpeg" => image.Encode(SKEncodedImageFormat.Jpeg, 85),
            "webp" => image.Encode(SKEncodedImageFormat.Webp, 85),
            _ => image.Encode(SKEncodedImageFormat.Png, 100),
        };
        return new SanitizedImage(data.ToArray(), $"image/{target}", resized.Width, resized.Height, format);
    }

    public byte[] BrandIcon(byte[] sanitizedLogo, string? background, int size = 64)
    {
        var inner = Math.Max(8, (int)Math.Floor((size * 0.74) + 0.5));
        using var logo = SKBitmap.Decode(sanitizedLogo) ?? throw Rejected("image-undecodable", "Le fichier n’est pas une image lisible.");
        var scale = Math.Min((double)inner / logo.Width, (double)inner / logo.Height);
        var width = Math.Max(1, (int)Math.Round(logo.Width * scale));
        var height = Math.Max(1, (int)Math.Round(logo.Height * scale));
        using var resized = logo.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKCubicResampler.Mitchell));
        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        var fill = background is not null && Regex.IsMatch(background.Trim(), "^#[0-9a-f]{6}$", RegexOptions.IgnoreCase)
            ? SKColor.Parse(background.Trim())
            : SKColors.Transparent;
        canvas.Clear(fill);
        using (var drawn = SKImage.FromBitmap(resized)) canvas.DrawImage(drawn, (size - width) / 2f, (size - height) / 2f, new SKSamplingOptions(SKCubicResampler.Mitchell));
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static SKBitmap? DecodeOriented(byte[] input)
    {
        using var codec = SKCodec.Create(new MemoryStream(input));
        if (codec is null) return null;
        var bitmap = SKBitmap.Decode(codec);
        if (bitmap is null) return null;
        // `.rotate()` de sharp : l'orientation EXIF est appliquée aux pixels, puis l'EXIF disparaît.
        return codec.EncodedOrigin switch
        {
            SKEncodedOrigin.BottomRight => Rotate(bitmap, 180),
            SKEncodedOrigin.RightTop => Rotate(bitmap, 90),
            SKEncodedOrigin.LeftBottom => Rotate(bitmap, 270),
            _ => bitmap,
        };
    }

    private static SKBitmap Rotate(SKBitmap source, int degrees)
    {
        var swap = degrees is 90 or 270;
        var rotated = new SKBitmap(swap ? source.Height : source.Width, swap ? source.Width : source.Height);
        using (var canvas = new SKCanvas(rotated))
        {
            canvas.Translate(rotated.Width / 2f, rotated.Height / 2f);
            canvas.RotateDegrees(degrees);
            canvas.Translate(-source.Width / 2f, -source.Height / 2f);
            using var drawn = SKImage.FromBitmap(source);
            canvas.DrawImage(drawn, 0, 0, new SKSamplingOptions(SKFilterMode.Linear));
        }
        source.Dispose();
        return rotated;
    }

    private static SKBitmap? RenderSvg(byte[] input)
    {
        using var svg = new Svg.Skia.SKSvg();
        using var stream = new MemoryStream(input);
        var picture = svg.Load(stream);
        if (picture is null || picture.CullRect.Width <= 0 || picture.CullRect.Height <= 0) return null;
        var width = (int)Math.Ceiling(picture.CullRect.Width * SvgScale);
        var height = (int)Math.Ceiling(picture.CullRect.Height * SvgScale);
        if (width > MaxInputSide || height > MaxInputSide) return new SKBitmap(MaxInputSide + 1, 1);
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(SvgScale);
        canvas.DrawPicture(picture);
        return bitmap;
    }

    /// <summary>Format réel d'après les octets, comme `sharp().metadata().format`.</summary>
    private static string? Sniff(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return "png";
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return "jpeg";
        if (bytes.Length >= 12 && Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP") return "webp";
        var head = Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 1024)).TrimStart('﻿', ' ', '\t', '\r', '\n');
        if (head.StartsWith("<svg", StringComparison.OrdinalIgnoreCase) || (head.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) && head.Contains("<svg", StringComparison.OrdinalIgnoreCase))) return "svg";
        using var codec = SKCodec.Create(new MemoryStream(bytes));
        return codec?.EncodedFormat switch
        {
            SKEncodedImageFormat.Gif => "gif",
            SKEncodedImageFormat.Bmp => "bmp",
            SKEncodedImageFormat.Heif => "heif",
            null => null,
            var other => other.ToString()!.ToLowerInvariant(),
        };
    }

    private static BusinessException Rejected(string reason, string message) => BusinessException.Validation(message, new { reason });

    private static string SafeFolder(string folder) =>
        string.Join('/', folder.Split('/').Select(s => Regex.Replace(s, "[^a-zA-Z0-9_-]", string.Empty)).Where(s => s.Length > 0));

    private string PathOf(string key)
    {
        var root = Root;
        var target = Path.GetFullPath(Path.Combine(root, key));
        if (target != root && !target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw BusinessException.Validation("Clé de fichier invalide.");
        }
        return target;
    }
}
