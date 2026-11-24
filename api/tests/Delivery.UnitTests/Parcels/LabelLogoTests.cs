using Delivery.Infrastructure.Documents;
using SkiaSharp;

namespace Delivery.UnitTests.Parcels;

public sealed class LabelLogoTests
{
    private static byte[] Png(SKColor background, SKColor ink)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(40, 20, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        bitmap.Erase(background);
        for (var x = 10; x < 30; x++) for (var y = 5; y < 15; y++) bitmap.SetPixel(x, y, ink);
        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    private static SKColor Center(byte[] png)
    {
        using var bitmap = SKBitmap.Decode(png);
        return bitmap.GetPixel(20, 10);
    }

    [Fact]
    public void A_white_logo_on_transparency_is_blackened()
    {
        var logo = Png(SKColors.Transparent, SKColors.White);
        var center = Center(LabelLogo.ForLightBackground(logo)!);
        (center.Red, center.Green, center.Blue, center.Alpha).ShouldBe(((byte)0, (byte)0, (byte)0, (byte)255));
    }

    [Fact]
    public void A_dark_logo_on_transparency_is_kept()
    {
        var logo = Png(SKColors.Transparent, new SKColor(20, 40, 120));
        LabelLogo.ForLightBackground(logo).ShouldBeSameAs(logo);
    }

    [Fact]
    public void An_opaque_logo_on_a_dark_background_becomes_black_ink_on_white()
    {
        var logo = Png(new SKColor(10, 10, 60), SKColors.White);
        var result = LabelLogo.ForLightBackground(logo)!;
        Center(result).Red.ShouldBe((byte)0);
        using var bitmap = SKBitmap.Decode(result);
        bitmap.GetPixel(1, 1).Red.ShouldBe((byte)255);
    }
}
