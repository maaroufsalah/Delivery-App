using SkiaSharp;

namespace Delivery.Infrastructure.Documents;

/// <summary>
/// `logoForLightBackground` : la bande du logo est claire sur l'étiquette. Un logo clair sur transparence y est noirci ;
/// un logo opaque est réduit à deux tons (encre noire sur blanc, d'après le fond lu sur ses bords) ; un logo déjà
/// foncé est rendu tel quel. 600 px au plus de côté.
/// </summary>
internal static class LabelLogo
{
    private const int MaxSide = 600;

    public static byte[]? ForLightBackground(byte[]? input)
    {
        if (input is not { Length: > 0 }) return null;
        try
        {
            using var decoded = SKBitmap.Decode(input);
            if (decoded is null) return input;
            var scale = Math.Min(1f, Math.Min((float)MaxSide / decoded.Width, (float)MaxSide / decoded.Height));
            var width = Math.Max(1, (int)MathF.Round(decoded.Width * scale));
            var height = Math.Max(1, (int)MathF.Round(decoded.Height * scale));
            using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            using (var resized = decoded.Resize(bitmap.Info, new SKSamplingOptions(SKFilterMode.Linear))) resized?.CopyTo(bitmap);
            var pixels = bitmap.Pixels;
            static double Luminance(SKColor c) => (0.2126 * c.Red + 0.7152 * c.Green + 0.0722 * c.Blue) / 255;
            var visible = 0;
            var lightness = 0.0;
            foreach (var pixel in pixels)
            {
                if (pixel.Alpha < 16) continue;
                visible++;
                lightness += Luminance(pixel);
            }
            if (visible == 0) return input;
            var transparent = (pixels.Length - visible) / (double)pixels.Length >= 0.05;
            if (transparent)
            {
                if (lightness / visible < 0.6) return input;
                for (var i = 0; i < pixels.Length; i++) pixels[i] = new SKColor(0, 0, 0, pixels[i].Alpha);
            }
            else
            {
                var edge = 0.0;
                var count = 0;
                for (var x = 0; x < width; x++)
                {
                    edge += Luminance(pixels[x]) + Luminance(pixels[(height - 1) * width + x]);
                    count += 2;
                }
                var darkBackground = edge / Math.Max(count, 1) < 0.5;
                for (var i = 0; i < pixels.Length; i++)
                {
                    var ink = darkBackground ? Luminance(pixels[i]) >= 0.5 : Luminance(pixels[i]) < 0.5;
                    var value = (byte)(ink ? 0 : 255);
                    pixels[i] = new SKColor(value, value, value, 255);
                }
            }
            bitmap.Pixels = pixels;
            using var image = SKImage.FromBitmap(bitmap);
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            return png.ToArray();
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            return input;
        }
    }
}
