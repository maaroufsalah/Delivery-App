using System.Net;
using System.Net.Http.Json;
using Delivery.ContractTests.Infrastructure;
using Delivery.Domain.Enums;
using SkiaSharp;

namespace Delivery.ContractTests.Referential;

[Collection(ApiCollection.Name)]
public sealed class SettingsContractTests(ApiFactory factory)
{
    private async Task<HttpClient> AdminAsync() => await TestData.ClientForAsync(factory, await TestData.UserAsync(factory, RoleName.SUPER_ADMIN));

    [Fact]
    public async Task Company_patch_keeps_absent_fields_and_clears_nulls()
    {
        var admin = await AdminAsync();
        var before = await (await admin.GetAsync("/api/settings/company")).JsonAsync();
        before.GetProperty("id").GetString().ShouldBe("cmp-wimo");
        before.GetProperty("theme").EnumerateObject().ShouldBeEmpty();

        var patched = await (await admin.PatchAsJsonAsync("/api/settings/company", new { email = " CONTACT@Wimo.ma ", website = (string?)null })).JsonAsync();
        patched.GetProperty("email").GetString().ShouldBe("contact@wimo.ma");
        patched.GetProperty("website").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        patched.GetProperty("name").GetString().ShouldBe(before.GetProperty("name").GetString());

        var invalid = await admin.PatchAsJsonAsync("/api/settings/company", new { primaryColor = "orange", locales = new[] { "fr" }, defaultLocale = "ar" });
        invalid.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var issue = (await invalid.JsonAsync()).GetProperty("details").GetProperty("issues")[0];
        issue.GetProperty("path").GetString().ShouldBe("primaryColor");
        issue.GetProperty("message").GetString().ShouldBe("invalid-color");
    }

    [Fact]
    public async Task Theme_is_validated_for_contrast_and_reserved_to_super_admin()
    {
        var admin = await AdminAsync();
        var unreadable = await admin.PatchAsJsonAsync("/api/settings/theme", new { light = new { header = new { background = "#ffffff", foreground = "#eeeeee" } } });
        unreadable.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var issue = (await unreadable.JsonAsync()).GetProperty("details").GetProperty("issues")[0];
        issue.GetProperty("path").GetString().ShouldBe("light.header.foreground");
        issue.GetProperty("message").GetString().ShouldBe("contrast-too-low");

        var saved = await (await admin.PatchAsJsonAsync("/api/settings/theme", new { light = new { sidebar = new { background = "#0f172a", foreground = "#e2e8f0", accent = "" } } })).JsonAsync();
        saved.GetProperty("theme").GetProperty("light").GetProperty("sidebar").GetProperty("background").GetString().ShouldBe("#0F172A");
        saved.GetProperty("theme").GetProperty("light").GetProperty("sidebar").TryGetProperty("accent", out _).ShouldBeFalse();

        var workbench = await (await admin.GetAsync("/api/settings/theme")).JsonAsync();
        workbench.GetProperty("palettes").GetArrayLength().ShouldBe(11);
        workbench.GetProperty("previous").GetProperty("theme").EnumerateObject().ShouldBeEmpty();

        var restored = await admin.PostAsync("/api/settings/theme/restore/previous", null);
        restored.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await restored.JsonAsync()).GetProperty("theme").EnumerateObject().ShouldBeEmpty();
        (await admin.PostAsync("/api/settings/theme/restore/nope", null)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Finance_and_business_settings_round_trip()
    {
        var admin = await AdminAsync();
        var finance = await (await admin.GetAsync("/api/settings/finance")).JsonAsync();
        finance.GetProperty("pickupFee").GetDouble().ShouldBe(20);
        finance.GetProperty("invoicePrefix").GetString().ShouldBe("FA");

        var updated = await admin.PatchAsJsonAsync("/api/settings/finance", new
        {
            vatRate = 20, invoicePrefix = "fx", remittancePrefix = "RF", statementPrefix = "PAI", codPayoutPrefix = "BV", numberingYearlyReset = true,
            failureFeeSameCity = 0, failureFeeCrossCity = 10, pickupFee = 15, freePickupThreshold = 3, courierReturnFee = 5, courierFailedCommission = 0,
            courierPayPeriod = "weekly", invoiceFooter = "Merci", codPayoutFooter = "", defaultBankAccountId = (string?)null, auditRetentionDays = 0,
        });
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await updated.JsonAsync();
        body.GetProperty("invoicePrefix").GetString().ShouldBe("FX");
        body.GetProperty("pickupFee").GetDouble().ShouldBe(15);
        body.GetProperty("vatRate").GetDouble().ShouldBe(20);

        var business = await admin.PatchAsJsonAsync("/api/settings/business", new
        {
            parcelCodePrefix = "wm", localesEnabled = new[] { "fr", "ar" }, localesDefault = "ar", claimResolutionDays = "7",
            labelTemplate = "branded", labelBrandColor = "", labelBanner = "Wimo",
        });
        var businessBody = await business.JsonAsync();
        businessBody.GetProperty("parcelCodePrefix").GetString().ShouldBe("WM");
        businessBody.GetProperty("claimResolutionDays").GetDouble().ShouldBe(7);

        var mismatch = await admin.PatchAsJsonAsync("/api/settings/business", new
        {
            parcelCodePrefix = "", localesEnabled = new[] { "fr" }, localesDefault = "en", claimResolutionDays = 1, labelTemplate = "classic", labelBrandColor = "", labelBanner = "",
        });
        mismatch.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await mismatch.JsonAsync()).GetProperty("details").GetProperty("field").GetString().ShouldBe("localesDefault");
    }

    [Fact]
    public async Task Logo_upload_is_sanitized_and_served_publicly()
    {
        var admin = await AdminAsync();
        using var bitmap = new SKBitmap(1024, 512);
        using (var canvas = new SKCanvas(bitmap)) canvas.Clear(SKColors.OrangeRed);
        using var image = SKImage.FromBitmap(bitmap);
        var png = image.Encode(SKEncodedImageFormat.Png, 100).ToArray();

        var mismatch = await admin.PostAsJsonAsync("/api/settings/logo", new { name = "logo.jpg", mimeType = "image/jpeg", content = Convert.ToBase64String(png) });
        (await mismatch.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("image-type-mismatch");

        var uploaded = await admin.PostAsJsonAsync("/api/settings/logo", new { name = "logo.png", mimeType = "image/png", content = Convert.ToBase64String(png) });
        uploaded.StatusCode.ShouldBe(HttpStatusCode.Created);
        var company = await uploaded.JsonAsync();
        var logo = company.GetProperty("logo").GetString()!;
        logo.ShouldStartWith("/api/public/logo/");
        company.GetProperty("icon").GetString()!.ShouldStartWith("/api/public/logo/");

        var served = await factory.CreateClient().GetAsync(logo);
        served.StatusCode.ShouldBe(HttpStatusCode.OK);
        served.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        served.Headers.CacheControl!.ToString().ShouldContain("immutable");
        using var decoded = SKBitmap.Decode(await served.Content.ReadAsByteArrayAsync());
        decoded.Width.ShouldBe(512);
        decoded.Height.ShouldBe(256);
    }

    [Fact]
    public async Task Public_routes_need_no_session()
    {
        var client = factory.CreateClient();
        var company = await (await client.GetAsync("/api/public/company")).JsonAsync();
        company.GetProperty("proLegalName").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        company.TryGetProperty("environmentLabel", out var label).ShouldBeTrue();
        label.ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);

        var rates = await (await client.GetAsync("/api/public/rates")).JsonAsync();
        rates.EnumerateArray().ShouldContain(r => r.GetProperty("cityName").GetString() == "Casablanca");
        var config = await (await client.GetAsync("/api/public/mobile-config")).JsonAsync();
        config.GetProperty("storeUrls").GetProperty("android").GetString().ShouldBe(string.Empty);
        (await (await client.GetAsync("/api/public/cities")).JsonAsync()).GetArrayLength().ShouldBeGreaterThan(0);
        (await client.GetAsync("/api/public/logo/absent.png")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
