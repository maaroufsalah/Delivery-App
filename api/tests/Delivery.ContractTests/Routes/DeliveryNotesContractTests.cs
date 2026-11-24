using System.Net;
using System.Net.Http.Json;
using Delivery.ContractTests.Infrastructure;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.ContractTests.Routes;

[Collection(ApiCollection.Name)]
public sealed class DeliveryNotesContractTests(ApiFactory factory)
{
    internal sealed record World(City City, Agency Agency, Client Merchant, HttpClient Seller, HttpClient Admin);

    internal static async Task<World> WorldAsync(ApiFactory factory)
    {
        var (_, city) = await TestData.CityAsync(factory);
        var agency = await TestData.AgencyAsync(factory, city.Id);
        var merchant = await TestData.MerchantAsync(factory, city.Id);
        var seller = await TestData.ClientForAsync(factory, new User { Id = merchant.UserId!, Email = merchant.Email });
        var admin = await TestData.ClientForAsync(factory, await TestData.UserAsync(factory, RoleName.SUPER_ADMIN));
        return new World(city, agency, merchant, seller, admin);
    }

    internal static async Task<(string Id, string Code)> ParcelAsync(World world, double cod = 100)
    {
        var parcel = await (await world.Seller.PostAsJsonAsync("/api/parcels", new
        {
            clientId = world.Merchant.Id, description = "Article", recipientName = "Nora", recipientPhone = "0611111111", originCityId = world.City.Id,
            pickupAddress = "Dépôt", destinationCityId = world.City.Id, deliveryAddress = "Rue 1", codAmount = cod,
        })).JsonAsync();
        return (parcel.GetProperty("id").GetString()!, parcel.GetProperty("code").GetString()!);
    }

    [Fact]
    public async Task Pickup_note_is_requested_validated_partially_and_its_fee_billed()
    {
        var world = await WorldAsync(factory);
        var first = await ParcelAsync(world);
        var second = await ParcelAsync(world);

        var requested = await world.Seller.PostAsJsonAsync("/api/merchant/me/pickups", new { parcelIds = new[] { first.Id, second.Id } });
        requested.StatusCode.ShouldBe(HttpStatusCode.OK);
        var note = await requested.JsonAsync();
        var code = note.GetProperty("code").GetString()!;
        code.ShouldStartWith("BR-");
        note.GetProperty("kind").GetString().ShouldBe("pickup");
        note.GetProperty("status").GetString().ShouldBe("awaiting-validation");
        note.GetProperty("parcelCount").GetInt32().ShouldBe(2);
        note.GetProperty("validatable").GetBoolean().ShouldBeTrue();
        note.GetProperty("pickupRule").GetProperty("fee").GetInt64().ShouldBe(2000);
        (await world.Seller.PostAsJsonAsync("/api/merchant/me/pickups", new { parcelIds = new[] { first.Id } })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await world.Seller.PostAsJsonAsync("/api/merchant/me/pickups", new { })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var stats = await (await world.Seller.GetAsync("/api/delivery-notes/stats")).JsonAsync();
        stats.GetProperty("total").GetInt32().ShouldBe(1);
        stats.GetProperty("awaiting").GetInt32().ShouldBe(1);
        stats.GetProperty("parcels").GetInt32().ShouldBe(2);

        var validated = await world.Admin.PostAsJsonAsync($"/api/delivery-notes/{code}/validate", new { parcelIds = new[] { first.Id } });
        validated.StatusCode.ShouldBe(HttpStatusCode.Created);
        var detail = await validated.JsonAsync();
        detail.GetProperty("status").GetString().ShouldBe("validated");
        detail.GetProperty("parcelCount").GetInt32().ShouldBe(1);
        detail.GetProperty("removedCount").GetInt32().ShouldBe(1);
        detail.GetProperty("removedParcels")[0].GetProperty("code").GetString().ShouldBe(second.Code);
        detail.GetProperty("pickupFeeCharged").GetInt64().ShouldBe(2000);
        (await (await world.Admin.GetAsync($"/api/parcels/{first.Code}")).JsonAsync()).GetProperty("statusId").GetInt32().ShouldBe(3);
        (await (await world.Admin.GetAsync($"/api/parcels/{second.Code}")).JsonAsync()).GetProperty("statusId").GetInt32().ShouldBe(1);

        await factory.ExecuteDbAsync(async db =>
        {
            var invoice = await db.Invoices.Include(i => i.Adjustments).SingleAsync(i => i.ClientId == world.Merchant.Id);
            invoice.Status.ShouldBe(InvoiceStatus.DRAFT);
            invoice.Adjustments.Single().Kind.ShouldBe(InvoiceAdjustmentKind.PICKUP_NOTE_FEE);
            invoice.Adjustments.Single().Amount.ShouldBe(20m);
            invoice.TotalAdjustment.ShouldBe(20m);
            invoice.TotalDue.ShouldBe(-20m);
        });

        (await world.Admin.PostAsJsonAsync($"/api/delivery-notes/{code}/validate", new { })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await world.Admin.DeleteAsync($"/api/delivery-notes/{code}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var notified = false;
        await factory.ExecuteDbAsync(async db => notified = await db.Notifications.AnyAsync(n => n.UserId == world.Merchant.UserId && n.Topic == "note-parcels-removed"));
        notified.ShouldBeTrue();

        var dropoff = await (await world.Seller.PostAsJsonAsync("/api/merchant/me/dropoffs", new { all = true })).JsonAsync();
        var dropCode = dropoff.GetProperty("code").GetString()!;
        dropCode.ShouldStartWith("BD-");
        (await (await world.Admin.GetAsync($"/api/parcels/{second.Code}")).JsonAsync()).GetProperty("statusId").GetInt32().ShouldBe(2);
        (await world.Seller.DeleteAsync($"/api/merchant/me/notes/{dropCode}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await world.Seller.GetAsync($"/api/delivery-notes/{dropCode}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var export = await world.Admin.GetAsync($"/api/delivery-notes/{code}/export");
        export.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await world.Admin.GetAsync($"/api/bulk-jobs?target={code}")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var other = await WorldAsync(factory);
        (await other.Seller.GetAsync($"/api/delivery-notes/{code}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await (await other.Seller.GetAsync("/api/delivery-notes")).JsonAsync()).GetProperty("total").GetInt32().ShouldBe(0);
    }
}
