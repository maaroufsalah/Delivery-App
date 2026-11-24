using System.Net;
using System.Net.Http.Json;
using Delivery.ContractTests.Infrastructure;

namespace Delivery.ContractTests.Routes;

/// <summary>`CourierMaskInterceptor` : le vendeur ne lit ni le nom du livreur ni celui du personnel.</summary>
[Collection(ApiCollection.Name)]
public sealed class CourierMaskContractTests(ApiFactory factory)
{
    [Fact]
    public async Task Merchant_reads_the_courier_and_staff_under_aliases()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var (courier, phone) = await RoutesContractTests.CourierAsync(factory, world.City.Id);
        var parcel = await DeliveryNotesContractTests.ParcelAsync(world);
        await RoutesContractTests.ReadyAsync(factory, parcel.Id);
        var code = (await (await world.Admin.PostAsJsonAsync("/api/routes", new { cityId = world.City.Id, courierId = courier.Id })).JsonAsync()).GetProperty("code").GetString()!;
        await world.Admin.PostAsJsonAsync($"/api/routes/{code}/parcels", new { parcelIds = new[] { parcel.Id } });
        await world.Admin.PostAsync($"/api/routes/{code}/dispatch", null);
        (await phone.PostAsJsonAsync($"/api/courier/parcels/{parcel.Code}/status", new { statusId = 8, comment = $"Vu par {courier.FirstName} {courier.LastName}" })).StatusCode.ShouldBe(HttpStatusCode.OK);

        var courierName = $"{courier.FirstName} {courier.LastName}";
        var adminBody = await (await world.Admin.GetAsync($"/api/parcels/{parcel.Code}")).Content.ReadAsStringAsync();
        adminBody.ShouldContain(courierName);
        var sellerResponse = await world.Seller.GetAsync($"/api/parcels/{parcel.Code}");
        sellerResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sellerBody = await sellerResponse.Content.ReadAsStringAsync();
        sellerBody.ShouldNotContain(courierName);
        sellerBody.ShouldContain("Livreur ");
        var adminLast = (await (await world.Admin.GetAsync("/api/auth/me")).JsonAsync()).GetProperty("lastName").GetString()!;
        sellerBody.ShouldNotContain(adminLast);
    }
}
