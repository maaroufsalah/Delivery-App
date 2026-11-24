using System.Net;
using System.Net.Http.Json;
using Delivery.ContractTests.Infrastructure;
using Delivery.ContractTests.Routes;
using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Delivery.ContractTests.Finance;

/// <summary>`ChequesController` : le parcours d'un chèque reçu à la livraison, ses rejets et ses bons de remise.</summary>
[Collection(ApiCollection.Name)]
public sealed class ChequesContractTests(ApiFactory factory)
{
    private static async Task<(DeliveryNotesContractTests.World World, Courier Courier, HttpClient Phone, string ChequeId)> ChequeAsync(ApiFactory factory)
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var (courier, phone) = await RoutesContractTests.CourierAsync(factory, world.City.Id);
        await factory.ExecuteDbAsync(async db =>
        {
            db.Couriers.Attach(courier);
            courier.AgencyId = world.Agency.Id;
            db.CourierCities.Add(new CourierCity { CourierId = courier.Id, CityId = world.City.Id, DeliveryFee = 15m });
            await db.SaveChangesAsync();
        });
        var parcel = await DeliveryNotesContractTests.ParcelAsync(world, 300);
        await RoutesContractTests.ReadyAsync(factory, parcel.Id);
        var route = (await (await world.Admin.PostAsJsonAsync("/api/routes", new { cityId = world.City.Id, courierId = courier.Id })).JsonAsync()).GetProperty("code").GetString()!;
        await world.Admin.PostAsJsonAsync($"/api/routes/{route}/parcels", new { parcelIds = new[] { parcel.Id } });
        await world.Admin.PostAsync($"/api/routes/{route}/dispatch", null);
        var delivered = await phone.PostAsJsonAsync($"/api/courier/parcels/{parcel.Code}/status", new { statusId = 4, chequeAmount = 100, chequeNumber = "CH-0042", chequeBank = "CIH" });
        delivered.StatusCode.ShouldBe(HttpStatusCode.OK);
        var id = await factory.QueryDbAsync(db => db.Cheques.Where(c => c.ParcelId == parcel.Id).Select(c => c.Id).SingleAsync());
        return (world, courier, phone, id);
    }

    [Fact]
    public async Task Cheque_moves_along_its_flow_and_a_rejection_needs_a_reason()
    {
        var (world, courier, phone, id) = await ChequeAsync(factory);

        var page = await (await world.Admin.GetAsync($"/api/cheques?courierId={courier.Id}&status=en-cours")).JsonAsync();
        page.GetProperty("total").GetInt32().ShouldBe(1);
        page.GetProperty("totalAmount").GetInt64().ShouldBe(10000);
        var mine = await (await phone.GetAsync($"/api/cheques/{id}")).JsonAsync();
        mine.GetProperty("status").GetString().ShouldBe("recu-livreur");
        mine.GetProperty("nextStatus").GetString().ShouldBe("remis-admin");
        mine.GetProperty("rejectable").GetBoolean().ShouldBeFalse();
        var seen = await (await world.Seller.GetAsync($"/api/cheques/{id}")).JsonAsync();
        seen.GetProperty("courierId").GetString().ShouldBe(string.Empty);
        seen.GetProperty("nextStatus").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        (await world.Seller.PostAsJsonAsync($"/api/cheques/{id}/advance", new { })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var handed = await phone.PostAsJsonAsync($"/api/cheques/{id}/advance", new { comment = "Déposé au comptoir" });
        handed.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await handed.JsonAsync()).GetProperty("status").GetString().ShouldBe("remis-admin");
        (await phone.PostAsJsonAsync($"/api/cheques/{id}/advance", new { })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var bulk = await (await world.Admin.PostAsJsonAsync("/api/cheques/bulk-advance", new { ids = new[] { id, "inconnu" }, status = "recu-admin" })).JsonAsync();
        bulk.GetProperty("done")[0].GetString().ShouldBe(id);
        bulk.GetProperty("skipped")[0].GetProperty("reason").GetString().ShouldBe("cheque-unreachable");
        var wrong = await (await world.Admin.PostAsJsonAsync("/api/cheques/bulk-advance", new { ids = new[] { id }, status = "recu-admin" })).JsonAsync();
        wrong.GetProperty("skipped")[0].GetProperty("reason").GetString().ShouldBe("cheque-wrong-status");

        var noReason = await world.Admin.PostAsJsonAsync($"/api/cheques/{id}/reject", new { });
        noReason.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await noReason.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("cheque-reason-required");
        var rejected = await (await world.Admin.PostAsJsonAsync($"/api/cheques/{id}/reject", new { comment = "Sans provision" })).JsonAsync();
        rejected.GetProperty("status").GetString().ShouldBe("rejete");
        rejected.GetProperty("rejectionReason").GetString().ShouldBe("Sans provision");
        rejected.GetProperty("cancellable").GetBoolean().ShouldBeFalse();
        (await world.Admin.PostAsJsonAsync($"/api/cheques/{id}/cancel", new { })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        rejected.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("status").GetString()).ShouldBe(["recu-livreur", "remis-admin", "recu-admin", "rejete"]);
        (await world.Admin.GetAsync("/api/cheques/export")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Slips_are_printed_by_the_agency_and_traced_on_the_cheque()
    {
        var (world, _, _, id) = await ChequeAsync(factory);

        (await world.Seller.PostAsync($"/api/cheques/{id}/slip", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var single = await world.Admin.PostAsync($"/api/cheques/{id}/slip", null);
        single.StatusCode.ShouldBe(HttpStatusCode.Created);
        single.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        single.Content.Headers.ContentDisposition!.FileName!.Trim('"').ShouldStartWith("BRC-");
        var group = await world.Admin.PostAsJsonAsync("/api/cheques/slip", new { ids = new[] { id } });
        group.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await world.Admin.PostAsJsonAsync("/api/cheques/slip", new { ids = Array.Empty<string>() })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await world.Admin.PostAsJsonAsync("/api/cheques/slip", new { ids = new[] { "inconnu" } })).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var events = (await (await world.Admin.GetAsync($"/api/cheques/{id}")).JsonAsync()).GetProperty("events").EnumerateArray().ToList();
        events.Count(e => e.GetProperty("kind").GetString() == "slip").ShouldBe(2);
        events.Last().GetProperty("comment").GetString()!.ShouldStartWith("Bon de remise groupé BRC");
    }
}
