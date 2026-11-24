using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Delivery.ContractTests.Infrastructure;
using Delivery.ContractTests.Routes;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.ContractTests.Engagement;

/// <summary>`NotificationsController` (`me`) : l'appareil, le centre de notifications et les réglages par sujet.</summary>
[Collection(ApiCollection.Name)]
public sealed class NotificationsContractTests(ApiFactory factory)
{
    private static HttpRequestMessage Forget(string token) =>
        new(HttpMethod.Delete, "/api/me/push-token") { Content = JsonContent.Create(new { token }) };

    internal static Task<string> NotifyAsync(ApiFactory factory, string userId, string topic, object? data = null) =>
        factory.QueryDbAsync(async db =>
        {
            var row = new Notification { UserId = userId, Topic = topic, Params = "{\"code\":\"X-1\",\"count\":2}", Data = JsonSerializer.Serialize(data ?? new { parcelCode = "X-1" }) };
            db.Notifications.Add(row);
            await db.SaveChangesAsync();
            return row.Id;
        });

    [Fact]
    public async Task Device_registers_once_and_is_forgotten_on_logout()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var token = $"ExponentPushToken[{Guid.NewGuid():N}]";

        var invalid = await world.Seller.PostAsJsonAsync("/api/me/push-token", new { token = "pas-un-jeton", platform = "ios" });
        invalid.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await invalid.JsonAsync()).GetRawText().ShouldContain("invalid-push-token");
        (await world.Seller.PostAsJsonAsync("/api/me/push-token", new { token, platform = "windows" })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var registered = await world.Seller.PostAsJsonAsync("/api/me/push-token", new { token = $"  {token} ", platform = "android", locale = "ar", appVersion = "2.1.0" });
        registered.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await registered.JsonAsync()).GetProperty("token").GetString().ShouldBe(token);
        (await world.Seller.PostAsJsonAsync("/api/me/push-token", new { token, platform = "ios" })).StatusCode.ShouldBe(HttpStatusCode.OK);
        var row = await factory.QueryDbAsync(db => db.PushTokens.Where(t => t.Token == token).ToListAsync());
        row.Count.ShouldBe(1);
        row[0].Platform.ShouldBe(DevicePlatform.IOS);
        row[0].Locale.ShouldBe("fr");

        (await (await world.Admin.SendAsync(Forget(token))).JsonAsync()).GetProperty("removed").GetInt32().ShouldBe(0);
        var forgotten = await world.Seller.SendAsync(Forget(token));
        forgotten.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await forgotten.JsonAsync()).GetProperty("removed").GetInt32().ShouldBe(1);
        (await world.Seller.SendAsync(Forget(" "))).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Settings_list_the_audience_topics_and_refuse_critical_or_foreign_ones()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var settings = await (await world.Seller.GetAsync("/api/me/notifications")).JsonAsync();
        settings.GetProperty("audience").GetString().ShouldBe("merchant");
        var topics = settings.GetProperty("topics").EnumerateArray().ToList();
        topics.Select(t => t.GetProperty("key").GetString()).ShouldContain("parcel-delivered");
        topics.Select(t => t.GetProperty("key").GetString()).ShouldNotContain("route-assigned");
        topics.Single(t => t.GetProperty("key").GetString() == "claim-answered").GetProperty("critical").GetBoolean().ShouldBeTrue();

        var off = await (await world.Seller.PatchAsJsonAsync("/api/me/notifications", new { topic = "parcel-delivered", enabled = false })).JsonAsync();
        off.GetProperty("topics").EnumerateArray().Single(t => t.GetProperty("key").GetString() == "parcel-delivered").GetProperty("enabled").GetBoolean().ShouldBeFalse();
        await world.Seller.PatchAsJsonAsync("/api/me/notifications", new { topic = "parcel-delivered", enabled = true });
        await world.Seller.PatchAsJsonAsync("/api/me/notifications", new { topic = "parcel-delivered", enabled = false });

        var foreign = await world.Seller.PatchAsJsonAsync("/api/me/notifications", new { topic = "route-assigned", enabled = false });
        foreign.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await foreign.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("topic-not-yours");
        var critical = await world.Seller.PatchAsJsonAsync("/api/me/notifications", new { topic = "claim-answered", enabled = false });
        critical.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await critical.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("topic-critical");
        (await world.Seller.PatchAsJsonAsync("/api/me/notifications", new { topic = "inconnu", enabled = false })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await world.Seller.PatchAsJsonAsync("/api/me/notifications", new { topic = "parcel-delivered" })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        // La préférence coupée retient la ligne ; un sujet critique passe toujours.
        await factory.ExecuteDbAsync(async db =>
        {
            await Delivery.Application.Notifications.Notifier.NotifyMerchantAsync(db, world.Merchant.Id, "parcel-delivered", new Dictionary<string, object> { ["code"] = "P-1" },
                new Dictionary<string, string>(), CancellationToken.None);
            await Delivery.Application.Notifications.Notifier.NotifyMerchantAsync(db, world.Merchant.Id, "parcel-rerouted", new Dictionary<string, object> { ["code"] = "P-1" },
                new Dictionary<string, string>(), CancellationToken.None);
        });
        var stored = await factory.QueryDbAsync(db => db.Notifications.Where(n => n.UserId == world.Merchant.UserId).Select(n => n.Topic).ToListAsync());
        stored.ShouldNotContain("parcel-delivered");
        stored.ShouldContain("parcel-rerouted");

        var admin = await (await world.Admin.GetAsync("/api/me/notifications")).JsonAsync();
        admin.GetProperty("audience").GetString().ShouldBe("staff");
    }

    [Fact]
    public async Task Feed_counts_unread_and_marks_them_read()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var first = await NotifyAsync(factory, world.Merchant.UserId!, "parcel-delivered");
        await NotifyAsync(factory, world.Merchant.UserId!, "parcel-failed");

        var feed = await (await world.Seller.GetAsync("/api/me/notification-feed")).JsonAsync();
        feed.GetProperty("unread").GetInt32().ShouldBe(2);
        var item = feed.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetString() == first);
        item.GetProperty("params").GetProperty("count").GetInt32().ShouldBe(2);
        item.GetProperty("data").GetProperty("parcelCode").GetString().ShouldBe("X-1");
        item.GetProperty("readAt").ValueKind.ShouldBe(JsonValueKind.Null);

        var read = await world.Seller.PostAsync($"/api/me/notification-feed/{first}/read", null);
        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await read.JsonAsync()).GetProperty("unread").GetInt32().ShouldBe(1);
        (await (await world.Seller.PostAsync("/api/me/notification-feed/read-all", null)).JsonAsync()).GetProperty("unread").GetInt32().ShouldBe(0);
        (await (await world.Seller.GetAsync("/api/me/notification-feed")).JsonAsync()).GetProperty("items")[0].GetProperty("readAt").ValueKind.ShouldBe(JsonValueKind.String);
    }

    [Fact]
    public async Task Courier_no_longer_sees_the_assignment_of_a_closed_route()
    {
        var (_, city) = await TestData.CityAsync(factory);
        var (courier, phone) = await RoutesContractTests.CourierAsync(factory, city.Id);
        var closed = new Route { Code = TestData.Unique("CIR-"), Status = RouteStatus.CLOSED, CityId = city.Id, OriginCityId = city.Id, CourierId = courier.Id };
        var open = new Route { Code = TestData.Unique("CIR-"), Status = RouteStatus.DELIVERING, CityId = city.Id, OriginCityId = city.Id, CourierId = courier.Id };
        await factory.ExecuteDbAsync(async db =>
        {
            db.Routes.AddRange(closed, open);
            await db.SaveChangesAsync();
        });
        var hidden = await NotifyAsync(factory, courier.UserId!, "route-assigned", new { routeCode = closed.Code });
        var shown = await NotifyAsync(factory, courier.UserId!, "route-assigned", new { routeCode = open.Code });

        var feed = await (await phone.GetAsync("/api/me/notification-feed")).JsonAsync();
        var ids = feed.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString()).ToList();
        ids.ShouldContain(shown);
        ids.ShouldNotContain(hidden);
        feed.GetProperty("unread").GetInt32().ShouldBe(1);
        (await (await phone.GetAsync("/api/me/notifications")).JsonAsync()).GetProperty("audience").GetString().ShouldBe("courier");
    }

    [Fact]
    public async Task Stored_notification_is_queued_for_its_push()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var id = await NotifyAsync(factory, world.Merchant.UserId!, "parcel-delivered");
        var queue = factory.Services.GetRequiredService<PushQueue>();
        var queued = new List<string>();
        while (queue.Reader.TryRead(out var next)) queued.Add(next);
        queued.ShouldContain(id);
    }
}
