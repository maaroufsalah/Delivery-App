using System.Net;
using System.Net.Http.Json;
using Delivery.ContractTests.Infrastructure;
using Delivery.ContractTests.Routes;

namespace Delivery.ContractTests.Engagement;

/// <summary>`AnnouncementsController` : la rédaction publie, chaque audience lit ce qui la concerne.</summary>
[Collection(ApiCollection.Name)]
public sealed class AnnouncementsContractTests(ApiFactory factory)
{
    internal const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    internal static object Body(string title, string audience, bool published, object? image = null) =>
        new { title, intro = "", body = "Un corps suffisamment long.", audience, isPublished = published, image };

    [Fact]
    public async Task Announcement_is_drafted_published_illustrated_and_withdrawn()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var (_, phone) = await RoutesContractTests.CourierAsync(factory, world.City.Id);
        var title = TestData.Unique("Nouveauté ");

        var invalid = await world.Admin.PostAsJsonAsync("/api/announcements", Body("ab", "all", true));
        invalid.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await invalid.JsonAsync()).GetRawText().ShouldContain("too-short");
        (await world.Seller.PostAsJsonAsync("/api/announcements", Body(title, "all", true))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var draft = await world.Admin.PostAsJsonAsync("/api/announcements", Body(title, "merchants", false));
        draft.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await draft.JsonAsync();
        var id = created.GetProperty("id").GetString()!;
        created.GetProperty("isPublished").GetBoolean().ShouldBeFalse();
        created.GetProperty("publishedAt").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        created.GetProperty("intro").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        created.GetProperty("authorName").GetString()!.ShouldStartWith("Test ");
        (await world.Seller.GetAsync($"/api/announcements/{id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var published = await (await world.Admin.PatchAsJsonAsync($"/api/announcements/{id}", Body(title, "merchants", true, new { filename = "visuel.png", mimeType = "image/png", content = Png })))
            .JsonAsync();
        published.GetProperty("isPublished").GetBoolean().ShouldBeTrue();
        published.GetProperty("hasImage").GetBoolean().ShouldBeTrue();
        published.GetProperty("imageName").GetString().ShouldBe("visuel.png");
        published.GetProperty("publishedAt").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.String);

        (await (await world.Seller.GetAsync($"/api/announcements/{id}")).JsonAsync()).GetProperty("body").GetString().ShouldBe("Un corps suffisamment long.");
        var image = await world.Seller.GetAsync($"/api/announcements/{id}/image");
        image.StatusCode.ShouldBe(HttpStatusCode.OK);
        image.Headers.CacheControl!.Private.ShouldBeTrue();
        image.Headers.CacheControl.MaxAge.ShouldBe(TimeSpan.FromSeconds(300));
        image.Content.Headers.ContentDisposition!.FileName!.Trim('"').ShouldBe("visuel.png");
        (await phone.GetAsync($"/api/announcements/{id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var feed = await (await world.Seller.GetAsync("/api/announcements/feed?limit=50")).JsonAsync();
        feed.EnumerateArray().Select(a => a.GetProperty("id").GetString()).ShouldContain(id);
        (await (await phone.GetAsync("/api/announcements/feed?limit=50")).JsonAsync()).EnumerateArray().Select(a => a.GetProperty("id").GetString()).ShouldNotContain(id);
        (await (await world.Seller.GetAsync("/api/announcements/feed?limit=abc")).JsonAsync()).GetArrayLength().ShouldBeLessThanOrEqualTo(3);

        var list = await (await world.Admin.GetAsync($"/api/announcements?search={Uri.EscapeDataString(title)}&published=true")).JsonAsync();
        list.GetProperty("total").GetInt32().ShouldBe(1);
        list.GetProperty("pageSize").GetInt32().ShouldBe(20);
        list.GetProperty("items")[0].TryGetProperty("body", out _).ShouldBeFalse();
        (await world.Admin.GetAsync("/api/announcements?pageSize=2")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await world.Seller.GetAsync("/api/announcements")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var kept = await (await world.Admin.PatchAsJsonAsync($"/api/announcements/{id}", new { title, body = "Un corps suffisamment long.", audience = "merchants", isPublished = true }))
            .JsonAsync();
        kept.GetProperty("hasImage").GetBoolean().ShouldBeTrue();
        var removed = await (await world.Admin.PatchAsJsonAsync($"/api/announcements/{id}", Body(title, "merchants", true))).JsonAsync();
        removed.GetProperty("hasImage").GetBoolean().ShouldBeFalse();

        (await (await world.Admin.DeleteAsync($"/api/announcements/{id}")).JsonAsync()).GetProperty("id").GetString().ShouldBe(id);
        (await world.Seller.GetAsync($"/api/announcements/{id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await world.Admin.DeleteAsync($"/api/announcements/{id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
