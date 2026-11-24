using System.Net;
using System.Net.Http.Json;
using Delivery.ContractTests.Infrastructure;
using Delivery.ContractTests.Routes;
using Microsoft.EntityFrameworkCore;

namespace Delivery.ContractTests.Returns;

/// <summary>`BlController` : le BL récupéré à la livraison, puis le bon BL de bout en bout.</summary>
[Collection(ApiCollection.Name)]
public sealed class BlContractTests(ApiFactory factory)
{
    internal static async Task<(DeliveryNotesContractTests.World World, (string Id, string Code) Parcel, HttpClient Phone, string CourierName)> DeliveredWithBlAsync(ApiFactory factory)
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var (courier, phone) = await RoutesContractTests.CourierAsync(factory, world.City.Id);
        var parcel = await DeliveryNotesContractTests.ParcelAsync(world);
        await factory.ExecuteDbAsync(async db =>
        {
            (await db.Clients.SingleAsync(c => c.Id == world.Merchant.Id)).IsPro = true;
            var row = await db.Parcels.SingleAsync(p => p.Id == parcel.Id);
            row.BlRequested = true;
            row.StatusId = 3;
            await db.SaveChangesAsync();
        });
        var code = (await (await world.Admin.PostAsJsonAsync("/api/routes", new { cityId = world.City.Id, courierId = courier.Id })).JsonAsync()).GetProperty("code").GetString()!;
        await world.Admin.PostAsJsonAsync($"/api/routes/{code}/parcels", new { parcelIds = new[] { parcel.Id } });
        await world.Admin.PostAsync($"/api/routes/{code}/dispatch", null);
        (await phone.PostAsJsonAsync($"/api/courier/parcels/{parcel.Code}/status", new { statusId = 4, blCollected = true, blReference = "REF-7" })).StatusCode.ShouldBe(HttpStatusCode.OK);
        return (world, parcel, phone, $"{courier.FirstName} {courier.LastName}");
    }

    [Fact]
    public async Task Bl_collected_at_delivery_travels_on_a_note_until_closed()
    {
        var (world, parcel, _, courierName) = await DeliveredWithBlAsync(factory);

        var tracking = await (await world.Admin.GetAsync($"/api/bl/parcels/{parcel.Code}")).JsonAsync();
        tracking.GetProperty("requested").GetBoolean().ShouldBeTrue();
        var slip = tracking.GetProperty("slip");
        slip.GetProperty("status").GetString().ShouldBe("collected");
        slip.GetProperty("reference").GetString().ShouldBe("REF-7");
        slip.GetProperty("courierName").GetString().ShouldBe(courierName);
        slip.GetProperty("steps").GetArrayLength().ShouldBe(1);
        var slipId = slip.GetProperty("id").GetString()!;

        var seen = await (await world.Seller.GetAsync($"/api/bl/parcels/{parcel.Code}")).JsonAsync();
        seen.GetProperty("slip").GetProperty("courierName").GetString()!.ShouldStartWith("Livreur");
        (await world.Seller.GetAsync("/api/bl/slips")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var free = await (await world.Admin.GetAsync($"/api/bl/slips?clientId={world.Merchant.Id}")).JsonAsync();
        free.EnumerateArray().Select(s => s.GetProperty("id").GetString()).ShouldContain(slipId);
        (await (await world.Seller.GetAsync("/api/bl/delivery-slips")).JsonAsync()).GetProperty("total").GetInt32().ShouldBe(1);

        var again = await world.Admin.PostAsJsonAsync($"/api/bl/parcels/{parcel.Code}/collect", new { collected = true });
        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await again.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("bl-already-collected");
        (await world.Admin.PostAsJsonAsync($"/api/bl/parcels/{parcel.Code}/collect", new { })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var created = await world.Admin.PostAsJsonAsync("/api/bl/notes", new { clientId = world.Merchant.Id, agencyId = world.Agency.Id, slipIds = new[] { slipId } });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var note = await created.JsonAsync();
        var code = note.GetProperty("code").GetString()!;
        code.ShouldStartWith("BRB");
        note.GetProperty("status").GetString().ShouldBe("draft");
        note.GetProperty("editable").GetBoolean().ShouldBeTrue();
        note.GetProperty("handoverable").GetBoolean().ShouldBeTrue();
        note.GetProperty("items").GetArrayLength().ShouldBe(1);
        (await (await world.Admin.GetAsync($"/api/bl/slips?clientId={world.Merchant.Id}")).JsonAsync()).EnumerateArray().Select(s => s.GetProperty("id").GetString()).ShouldNotContain(slipId);

        var revert = await world.Admin.PostAsJsonAsync($"/api/bl/delivery-slips/{slipId}/revert", new { reason = "erreur" });
        revert.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await revert.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("bl-revert-impossible");

        (await (await world.Admin.PostAsync($"/api/bl/notes/{code}/handover", null)).JsonAsync()).GetProperty("status").GetString().ShouldBe("awaiting-receipt");
        (await world.Admin.PostAsync($"/api/bl/notes/{code}/handover", null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await (await world.Admin.PostAsync($"/api/bl/notes/{code}/receive", null)).JsonAsync()).GetProperty("status").GetString().ShouldBe("received");
        var onNote = await world.Admin.PostAsJsonAsync($"/api/bl/delivery-slips/{slipId}/revert", new { reason = "erreur" });
        (await onNote.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("bl-on-note");
        (await world.Admin.PostAsJsonAsync($"/api/bl/notes/{code}/return", new { })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var returned = await (await world.Admin.PostAsJsonAsync($"/api/bl/notes/{code}/return", new { returnedToName = "Karim" })).JsonAsync();
        returned.GetProperty("status").GetString().ShouldBe("returned");
        returned.GetProperty("returnedToName").GetString().ShouldBe("Karim");
        returned.GetProperty("items")[0].GetProperty("status").GetString().ShouldBe("returned");
        var closed = await (await world.Admin.PostAsync($"/api/bl/notes/{code}/close", null)).JsonAsync();
        closed.GetProperty("status").GetString().ShouldBe("closed");
        closed.GetProperty("steps").GetArrayLength().ShouldBe(5);

        var pdf = await world.Seller.GetAsync($"/api/bl/notes/{code}/pdf");
        pdf.StatusCode.ShouldBe(HttpStatusCode.OK);
        pdf.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        (await pdf.Content.ReadAsByteArrayAsync())[..4].ShouldBe("%PDF"u8.ToArray());
        (await world.Admin.DeleteAsync($"/api/bl/notes/{code}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var topics = await factory.QueryDbAsync(db => db.Notifications.Where(n => n.UserId == world.Merchant.UserId).Select(n => n.Topic).ToListAsync());
        topics.ShouldContain("bl-ready");
        topics.ShouldContain("bl-returned");
        var events = await factory.QueryDbAsync(db => db.ParcelEvents.Where(e => e.ParcelId == parcel.Id && e.Comment != null).Select(e => e.Comment!).ToListAsync());
        events.ShouldContain("BL récupéré par le livreur — réf. REF-7");
        events.ShouldContain($"Bon BL clôturé — bon BL {code}");
    }

    [Fact]
    public async Task Slips_advance_by_selection_and_the_admin_reverts_a_step()
    {
        var (world, parcel, _, _) = await DeliveredWithBlAsync(factory);
        var slipId = (await (await world.Admin.GetAsync($"/api/bl/parcels/{parcel.Code}")).JsonAsync()).GetProperty("slip").GetProperty("id").GetString()!;

        (await world.Admin.PostAsJsonAsync("/api/bl/delivery-slips/advance", new { to = "returned", ids = new[] { slipId } })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var received = await world.Admin.PostAsJsonAsync("/api/bl/delivery-slips/advance", new { to = "received", ids = new[] { slipId } });
        received.StatusCode.ShouldBe(HttpStatusCode.Created);
        var result = await received.JsonAsync();
        result.GetProperty("count").GetInt32().ShouldBe(1);
        result.GetProperty("skipped").GetInt32().ShouldBe(0);

        var reverted = await world.Admin.PostAsJsonAsync($"/api/bl/delivery-slips/{slipId}/revert", new { reason = "mauvais lot" });
        reverted.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await reverted.JsonAsync();
        body.GetProperty("status").GetString().ShouldBe("collected");
        body.GetProperty("receivedAt").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        (await world.Seller.PostAsJsonAsync($"/api/bl/delivery-slips/{slipId}/revert", new { reason = "x" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
