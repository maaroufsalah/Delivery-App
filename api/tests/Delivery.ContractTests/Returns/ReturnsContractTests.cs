using System.Net;
using System.Net.Http.Json;
using Delivery.ContractTests.Infrastructure;
using Delivery.ContractTests.Routes;
using Delivery.Application.Common.Settings;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.ContractTests.Returns;

/// <summary>Le retour d'un colis de bout en bout : bon livreur (RL-), réception en agence, bon client (BRT-).</summary>
[Collection(ApiCollection.Name)]
public sealed class ReturnsContractTests(ApiFactory factory)
{
    internal static async Task<(DeliveryNotesContractTests.World World, (string Id, string Code) Parcel, Domain.Entities.Courier Courier, HttpClient Phone)> ToReturnAsync(ApiFactory factory,
        bool failed = false)
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var (courier, phone) = await RoutesContractTests.CourierAsync(factory, world.City.Id);
        var parcel = await DeliveryNotesContractTests.ParcelAsync(world);
        await factory.ExecuteDbAsync(async db =>
        {
            var row = await db.Parcels.SingleAsync(p => p.Id == parcel.Id);
            row.StatusId = 8;
            row.CourierId = courier.Id;
            row.AgencyId = world.Agency.Id;
            row.IsFailed = failed;
            row.FailureFee = null;
            await db.SaveChangesAsync();
        });
        return (world, parcel, courier, phone);
    }

    [Fact]
    public async Task Parcel_goes_back_to_the_agency_then_to_its_merchant()
    {
        var (world, parcel, courier, phone) = await ToReturnAsync(factory, failed: true);

        var pending = await (await world.Admin.GetAsync($"/api/returns/pending?courierId={courier.Id}")).JsonAsync();
        pending.EnumerateArray().Single().GetProperty("parcels").EnumerateArray().Select(p => p.GetProperty("code").GetString()).ShouldContain(parcel.Code);
        var candidates = await (await world.Admin.GetAsync($"/api/return-handovers/candidates?courierId={courier.Id}&page=1")).JsonAsync();
        candidates.GetProperty("total").GetInt32().ShouldBe(1);
        (await world.Seller.GetAsync("/api/return-handovers")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var created = await world.Admin.PostAsJsonAsync("/api/return-handovers", new { courierId = courier.Id, agencyId = world.Agency.Id, parcelIds = new[] { parcel.Id } });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var handover = await created.JsonAsync();
        var code = handover.GetProperty("code").GetString()!;
        code.ShouldStartWith("RL");
        handover.GetProperty("status").GetString().ShouldBe("return-requested");
        handover.GetProperty("pickable").GetBoolean().ShouldBeTrue();
        handover.GetProperty("clientNames")[0].GetString().ShouldBe(world.Merchant.Name);
        var again = await world.Admin.PostAsJsonAsync("/api/return-handovers", new { courierId = courier.Id, agencyId = world.Agency.Id, parcelIds = new[] { parcel.Id } });
        (await again.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("parcel-already-reserved");

        var wrong = await phone.PostAsJsonAsync($"/api/return-handovers/{code}/pickup", new { parcelCodes = new[] { "NOPE123" } });
        wrong.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await wrong.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("parcel-not-in-handover");
        (await (await phone.PostAsJsonAsync($"/api/return-handovers/{code}/pickup", new { parcelCodes = new[] { parcel.Code.ToLowerInvariant() } })).JsonAsync())
            .GetProperty("status").GetString().ShouldBe("picked-up");
        (await (await phone.PostAsync($"/api/return-handovers/{code}/handover", null)).JsonAsync()).GetProperty("status").GetString().ShouldBe("awaiting-receipt");
        var byCourier = await phone.PostAsync($"/api/return-handovers/{code}/confirm", null);
        byCourier.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var confirmed = await (await world.Admin.PostAsync($"/api/return-handovers/{code}/confirm", null)).JsonAsync();
        confirmed.GetProperty("status").GetString().ShouldBe("confirmed");
        confirmed.GetProperty("items")[0].GetProperty("statusId").GetInt32().ShouldBe(22);
        confirmed.GetProperty("closable").GetBoolean().ShouldBeTrue();
        (await (await world.Admin.PostAsync($"/api/return-handovers/{code}/close", null)).JsonAsync()).GetProperty("status").GetString().ShouldBe("closed");
        (await world.Admin.PostAsJsonAsync($"/api/return-handovers/{code}/cancel", new { reason = "x" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await world.Admin.GetAsync($"/api/return-handovers/{code}/pdf")).Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");

        var courierTopics = await factory.QueryDbAsync(db => db.Notifications.Where(n => n.UserId == courier.UserId).Select(n => n.Topic).ToListAsync());
        courierTopics.ShouldContain("return-note-requested");
        courierTopics.ShouldContain("return-note-received");

        // Bon client : le colis rentré, ses frais d'échec de la politique, puis la remise.
        var slip = await (await world.Admin.PostAsJsonAsync("/api/return-slips", new { clientId = world.Merchant.Id, agencyId = world.Agency.Id })).JsonAsync();
        var slipCode = slip.GetProperty("code").GetString()!;
        slipCode.ShouldStartWith("BRT");
        slip.GetProperty("status").GetString().ShouldBe("draft");
        (await (await world.Seller.GetAsync("/api/return-slips")).JsonAsync()).GetProperty("items").EnumerateArray().Select(s => s.GetProperty("code").GetString()).ShouldNotContain(slipCode);
        (await (await world.Admin.GetAsync($"/api/return-slips/{slipCode}/candidates")).JsonAsync()).EnumerateArray().Select(c => c.GetProperty("id").GetString()).ShouldContain(parcel.Id);
        var policy = await factory.QueryDbAsync(db => db.PricingPolicyAsync(CancellationToken.None, world.Agency.Id));
        var withParcel = await (await world.Admin.PostAsJsonAsync($"/api/return-slips/{slipCode}/parcels", new { parcelId = parcel.Id, reason = "refused" })).JsonAsync();
        withParcel.GetProperty("items")[0].GetProperty("reason").GetString().ShouldBe("refused");
        withParcel.GetProperty("items")[0].GetProperty("feeApplied").GetInt64().ShouldBe(policy.FailureFeeSameCity);
        withParcel.GetProperty("totalFees").GetInt64().ShouldBe(policy.FailureFeeSameCity);
        var twice = await world.Admin.PostAsJsonAsync($"/api/return-slips/{slipCode}/parcels", new { parcelId = parcel.Id });
        (await twice.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("parcel-already-returned");
        (await world.Admin.PostAsJsonAsync($"/api/return-slips/{slipCode}/parcels", new { parcelId = parcel.Id, reason = "lost" })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        (await world.Admin.PostAsJsonAsync($"/api/return-slips/{slipCode}/close", new { })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var before = await factory.QueryDbAsync(db => db.Parcels.Where(p => p.Id == parcel.Id).Select(p => p.FailureFee).SingleAsync());
        before.ShouldBeNull();
        var delivered = await (await world.Admin.PostAsJsonAsync($"/api/return-slips/{slipCode}/close", new { receivedByName = "Karim" })).JsonAsync();
        delivered.GetProperty("status").GetString().ShouldBe("delivered");
        delivered.GetProperty("receivedByName").GetString().ShouldBe("Karim");
        var after = await factory.QueryDbAsync(db => db.Parcels.Where(p => p.Id == parcel.Id).Select(p => new { p.StatusId, p.FailureFee }).SingleAsync());
        after.StatusId.ShouldBe(23);
        after.FailureFee.ShouldBe(policy.FailureFeeSameCity / 100m);
        (await world.Admin.DeleteAsync($"/api/return-slips/{slipCode}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var seller = await (await world.Seller.GetAsync("/api/return-slips")).JsonAsync();
        seller.GetProperty("items").EnumerateArray().Select(s => s.GetProperty("code").GetString()).ShouldContain(slipCode);
        (await world.Seller.GetAsync($"/api/return-slips/{slipCode}/pdf")).StatusCode.ShouldBe(HttpStatusCode.OK);
        var mine = await (await world.Seller.GetAsync("/api/returns/pending")).JsonAsync();
        mine.EnumerateArray().Single().GetProperty("parcels")[0].GetProperty("courierName").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task Courier_opens_his_own_handover_and_an_admin_draft_stays_closed_to_him()
    {
        var (world, parcel, courier, phone) = await ToReturnAsync(factory);

        (await phone.PostAsJsonAsync("/api/return-handovers", new { })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var draft = await (await world.Admin.PostAsJsonAsync("/api/return-handovers", new { courierId = courier.Id, agencyId = world.Agency.Id })).JsonAsync();
        var draftCode = draft.GetProperty("code").GetString()!;
        draft.GetProperty("status").GetString().ShouldBe("draft");
        var locked = await phone.PostAsJsonAsync($"/api/return-handovers/{draftCode}/parcels", new { code = parcel.Code });
        locked.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await locked.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("handover-admin-locked");
        (await world.Admin.DeleteAsync($"/api/return-handovers/{draftCode}")).StatusCode.ShouldBe(HttpStatusCode.OK);

        await factory.ExecuteDbAsync(async db =>
        {
            var row = await db.Couriers.SingleAsync(c => c.Id == courier.Id);
            row.AgencyId = world.Agency.Id;
            await db.SaveChangesAsync();
        });
        var own = await phone.PostAsJsonAsync("/api/return-handovers", new { parcelIds = new[] { parcel.Id } });
        own.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await own.JsonAsync();
        body.GetProperty("origin").GetString().ShouldBe("courier");
        body.GetProperty("editable").GetBoolean().ShouldBeTrue();
        body.GetProperty("confirmable").GetBoolean().ShouldBeFalse();
        var code = body.GetProperty("code").GetString()!;
        (await phone.PostAsJsonAsync($"/api/return-handovers/{code}/pickup", new { })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var cancelled = await (await phone.PostAsJsonAsync($"/api/return-handovers/{code}/cancel", new { reason = "erreur" })).JsonAsync();
        cancelled.GetProperty("status").GetString().ShouldBe("cancelled");
        cancelled.GetProperty("cancelReason").GetString().ShouldBe("erreur");
        (await (await world.Admin.GetAsync($"/api/return-handovers/candidates?courierId={courier.Id}")).JsonAsync()).GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Bulk_opens_one_handover_per_courier_and_is_idempotent()
    {
        var (world, parcel, courier, _) = await ToReturnAsync(factory);
        var preview = await (await world.Admin.GetAsync("/api/return-handovers/bulk")).JsonAsync();
        preview.GetProperty("parcels").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
        var first = await world.Admin.PostAsync("/api/return-handovers/bulk", null);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await first.JsonAsync()).GetProperty("codes").GetArrayLength().ShouldBeGreaterThanOrEqualTo(1);
        var reserved = await factory.QueryDbAsync(db => db.ReturnHandoverItems.Where(i => i.ReservedParcelId == parcel.Id).Select(i => i.Handover.CourierId).SingleAsync());
        reserved.ShouldBe(courier.Id);
        (await (await world.Admin.PostAsync("/api/return-handovers/bulk", null)).JsonAsync()).GetProperty("created").GetInt32().ShouldBe(0);
        (await world.Seller.PostAsync("/api/return-handovers/bulk", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Returns_stay_within_their_owner()
    {
        var a = await ToReturnAsync(factory);
        var b = await ToReturnAsync(factory);
        var handoverA = (await (await a.World.Admin.PostAsJsonAsync("/api/return-handovers", new { courierId = a.Courier.Id, agencyId = a.World.Agency.Id, parcelIds = new[] { a.Parcel.Id } }))
            .JsonAsync()).GetProperty("code").GetString()!;
        var slipA = (await (await a.World.Admin.PostAsJsonAsync("/api/return-slips", new { clientId = a.World.Merchant.Id, agencyId = a.World.Agency.Id })).JsonAsync()).GetProperty("code").GetString()!;

        (await b.Phone.GetAsync($"/api/return-handovers/{handoverA}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await b.Phone.GetAsync($"/api/return-handovers/{handoverA}/pdf")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await (await b.Phone.GetAsync("/api/return-handovers")).JsonAsync()).GetProperty("total").GetInt32().ShouldBe(0);
        (await b.Phone.GetAsync($"/api/return-slips/{slipA}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await b.World.Seller.GetAsync($"/api/return-slips/{slipA}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await b.World.Seller.GetAsync($"/api/return-slips/{slipA}/pdf")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var pending = await (await b.Phone.GetAsync("/api/returns/pending")).JsonAsync();
        pending.EnumerateArray().SelectMany(g => g.GetProperty("parcels").EnumerateArray()).Select(p => p.GetProperty("id").GetString()).ShouldNotContain(a.Parcel.Id);
        (await b.Phone.GetAsync($"/api/bl/parcels/{a.Parcel.Code}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var agentB = await TestData.ClientForAsync(factory, await TestData.UserAsync(factory, RoleName.STAFF, b.World.Agency.Id));
        var list = await agentB.GetAsync("/api/return-handovers");
        if (list.StatusCode == HttpStatusCode.OK)
        {
            (await list.JsonAsync()).GetProperty("items").EnumerateArray().Select(h => h.GetProperty("code").GetString()).ShouldNotContain(handoverA);
            (await agentB.GetAsync($"/api/return-handovers/{handoverA}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await agentB.GetAsync($"/api/return-slips/{slipA}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await agentB.PostAsJsonAsync("/api/return-slips", new { clientId = a.World.Merchant.Id, agencyId = a.World.Agency.Id })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
        else
        {
            list.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
    }
}
