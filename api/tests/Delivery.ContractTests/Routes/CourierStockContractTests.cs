using System.Net;
using System.Net.Http.Json;
using Delivery.ContractTests.Infrastructure;
using Delivery.Domain.Entities;

namespace Delivery.ContractTests.Routes;

[Collection(ApiCollection.Name)]
public sealed class CourierStockContractTests(ApiFactory factory)
{
    [Fact]
    public async Task Courier_stock_counts_parcels_in_hand_and_cash_not_yet_remitted()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var (courier, phone) = await RoutesContractTests.CourierAsync(factory, world.City.Id);
        await factory.ExecuteDbAsync(async db =>
        {
            db.CourierCities.Add(new CourierCity { CourierId = courier.Id, CityId = world.City.Id, DeliveryFee = 15m });
            await db.SaveChangesAsync();
        });
        var onRoad = await DeliveryNotesContractTests.ParcelAsync(world, cod: 80);
        var cash = await DeliveryNotesContractTests.ParcelAsync(world, cod: 150.5);
        await RoutesContractTests.ReadyAsync(factory, onRoad.Id);
        await RoutesContractTests.ReadyAsync(factory, cash.Id);
        var code = (await (await world.Admin.PostAsJsonAsync("/api/routes", new { cityId = world.City.Id, courierId = courier.Id })).JsonAsync()).GetProperty("code").GetString()!;
        await world.Admin.PostAsJsonAsync($"/api/routes/{code}/parcels", new { parcelIds = new[] { onRoad.Id, cash.Id } });
        await world.Admin.PostAsync($"/api/routes/{code}/dispatch", null);
        (await phone.PostAsJsonAsync($"/api/routes/{code}/parcels/{cash.Id}/confirm", new { statusId = 4 })).StatusCode.ShouldBe(HttpStatusCode.Created);

        var page = await (await world.Admin.GetAsync($"/api/courier-stock?search={Uri.EscapeDataString(courier.LastName)}&pageSize=200")).JsonAsync();
        var row = page.GetProperty("items").EnumerateArray().Single(r => r.GetProperty("courierId").GetString() == courier.Id);
        row.GetProperty("inHand").GetInt32().ShouldBe(1);
        row.GetProperty("outForDelivery").GetInt32().ShouldBe(1);
        row.GetProperty("deliveredUnremitted").GetInt32().ShouldBe(1);
        row.GetProperty("collectedUnremittedInCents").GetInt64().ShouldBe(15050);
        row.GetProperty("toCollectInCents").GetInt64().ShouldBe(8000);
        (await world.Admin.GetAsync("/api/courier-stock?sort=nope")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var detail = await (await world.Admin.GetAsync($"/api/courier-stock/{courier.Id}?sort=code&order=asc")).JsonAsync();
        detail.GetProperty("courier").GetProperty("id").GetString().ShouldBe(courier.Id);
        detail.GetProperty("parcels").GetProperty("total").GetInt32().ShouldBe(2);
        detail.GetProperty("byStatus").GetArrayLength().ShouldBe(2);
        detail.GetProperty("options").GetProperty("routes")[0].GetProperty("label").GetString().ShouldBe(code);
        var filtered = await (await world.Admin.GetAsync($"/api/courier-stock/{courier.Id}?statusId=4")).JsonAsync();
        filtered.GetProperty("counters").GetProperty("collectedUnremittedInCents").GetInt64().ShouldBe(15050);
        (await world.Admin.GetAsync("/api/courier-stock/export")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await world.Admin.GetAsync($"/api/courier-stock/parcels/export?courierId={courier.Id}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await world.Admin.GetAsync("/api/courier-stock/parcels/export")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await phone.GetAsync("/api/courier-stock")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var global = await world.Admin.GetAsync($"/api/courier-stock/pdf?search={Uri.EscapeDataString(courier.LastName)}");
        global.StatusCode.ShouldBe(HttpStatusCode.OK);
        global.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        global.Content.Headers.ContentDisposition!.FileName!.Trim('"').ShouldStartWith("Stock-Livreurs-");
        var own = await world.Admin.GetAsync($"/api/courier-stock/{courier.Id}/pdf");
        own.StatusCode.ShouldBe(HttpStatusCode.OK);
        own.Content.Headers.ContentDisposition!.FileName!.Trim('"').ShouldStartWith("Stock-Hamid-Circuit-");

        (await world.Admin.PostAsJsonAsync("/api/courier-stock/pdf-jobs", new { kind = "nope" })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var created = await world.Admin.PostAsJsonAsync("/api/courier-stock/pdf-jobs", new { kind = "courier", courierId = courier.Id });
        created.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var job = await created.JsonAsync();
        job.GetProperty("format").GetString().ShouldBe("stock");
        job.GetProperty("count").GetInt32().ShouldBe(2);
        var id = job.GetProperty("id").GetString();
        var status = "pending";
        for (var attempt = 0; attempt < 50 && status is "pending" or "running"; attempt++)
        {
            await Task.Delay(100);
            status = (await (await world.Admin.GetAsync($"/api/courier-stock/pdf-jobs/{id}")).JsonAsync()).GetProperty("status").GetString();
        }
        status.ShouldBe("ready");
        var file = await world.Admin.GetAsync($"/api/courier-stock/pdf-jobs/{id}/file");
        file.StatusCode.ShouldBe(HttpStatusCode.OK);
        file.Content.Headers.ContentDisposition!.FileName!.Trim('"').ShouldBe("situation-stock-livreur-2.pdf");
    }
}
