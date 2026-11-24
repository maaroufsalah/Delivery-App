using System.Net;
using System.Net.Http.Json;
using Delivery.ContractTests.Infrastructure;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.ContractTests.Routes;

[Collection(ApiCollection.Name)]
public sealed class RoutesContractTests(ApiFactory factory)
{
    internal static async Task<(Courier Courier, HttpClient Client)> CourierAsync(ApiFactory factory, string cityId)
    {
        var user = await TestData.UserAsync(factory, RoleName.COURIER);
        var courier = new Courier { UserId = user.Id, FirstName = "Hamid", LastName = "Circuit", Phone = "0600000019", CityId = cityId };
        await factory.ExecuteDbAsync(async db =>
        {
            db.Couriers.Add(courier);
            await db.SaveChangesAsync();
        });
        return (courier, await TestData.ClientForAsync(factory, user));
    }

    internal static Task ReadyAsync(ApiFactory factory, string parcelId) =>
        factory.ExecuteDbAsync(async db =>
        {
            var parcel = await db.Parcels.SingleAsync(p => p.Id == parcelId);
            parcel.StatusId = 3;
            await db.SaveChangesAsync();
        });

    [Fact]
    public async Task Out_for_delivery_places_the_parcel_on_a_circuit_instead_of_failing()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var (courier, _) = await CourierAsync(factory, world.City.Id);
        await factory.ExecuteDbAsync(async db =>
        {
            db.CourierCities.Add(new CourierCity { CourierId = courier.Id, CityId = world.City.Id, DeliveryFee = 15m });
            await db.SaveChangesAsync();
        });
        var parcel = await DeliveryNotesContractTests.ParcelAsync(world);
        await factory.ExecuteDbAsync(async db =>
        {
            (await db.Parcels.SingleAsync(p => p.Id == parcel.Id)).StatusId = 16;
            await db.SaveChangesAsync();
        });

        (await world.Admin.PostAsJsonAsync($"/api/parcels/{parcel.Code}/events", new { statusId = 13 })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var placed = await world.Admin.PostAsJsonAsync($"/api/parcels/{parcel.Code}/events", new { statusId = 13, courierId = courier.Id });
        placed.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await placed.JsonAsync()).GetProperty("statusId").GetInt32().ShouldBe(13);

        var detail = await (await world.Admin.GetAsync($"/api/parcels/{parcel.Code}")).JsonAsync();
        detail.GetProperty("statusId").GetInt32().ShouldBe(13);
        detail.GetProperty("courierId").GetString().ShouldBe(courier.Id);
        var routes = await (await world.Admin.GetAsync($"/api/routes?courierId={courier.Id}")).JsonAsync();
        routes.GetProperty("total").GetInt32().ShouldBe(1);
        var route = routes.GetProperty("items")[0];
        route.GetProperty("status").GetString().ShouldBe("draft");
        route.GetProperty("parcelCount").GetInt32().ShouldBe(1);
        var circuit = await (await world.Admin.GetAsync($"/api/routes/{route.GetProperty("code").GetString()}")).JsonAsync();
        circuit.GetProperty("parcels")[0].GetProperty("code").GetString().ShouldBe(parcel.Code);
    }

    [Fact]
    public async Task Dispatched_route_collects_cash_into_the_courier_remittance_and_closes_itself()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var (courier, courierClient) = await CourierAsync(factory, world.City.Id);
        await factory.ExecuteDbAsync(async db =>
        {
            db.CourierCities.Add(new CourierCity { CourierId = courier.Id, CityId = world.City.Id, DeliveryFee = 15m });
            await db.SaveChangesAsync();
        });
        var cash = await DeliveryNotesContractTests.ParcelAsync(world, cod: 150.5);
        var refused = await DeliveryNotesContractTests.ParcelAsync(world);
        await ReadyAsync(factory, cash.Id);
        await ReadyAsync(factory, refused.Id);
        var code = (await (await world.Admin.PostAsJsonAsync("/api/routes", new { cityId = world.City.Id, courierId = courier.Id })).JsonAsync()).GetProperty("code").GetString()!;
        (await world.Admin.PostAsJsonAsync($"/api/routes/{code}/parcels", new { parcelIds = new[] { cash.Id, refused.Id } })).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await courierClient.PostAsJsonAsync($"/api/routes/{code}/parcels/{cash.Id}/confirm", new { statusId = 4 })).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var dispatched = await world.Admin.PostAsync($"/api/routes/{code}/dispatch", null);
        dispatched.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await dispatched.JsonAsync()).GetProperty("status").GetString().ShouldBe("dispatched");
        (await (await world.Admin.GetAsync($"/api/parcels/{cash.Code}")).JsonAsync()).GetProperty("statusId").GetInt32().ShouldBe(13);
        (await world.Admin.PostAsync($"/api/routes/{code}/dispatch", null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await world.Admin.PostAsJsonAsync($"/api/routes/{code}/close", new { })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        (await courierClient.PostAsJsonAsync($"/api/routes/{code}/parcels/{cash.Id}/confirm", new { statusId = 4, collectedAmount = 10 })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await courierClient.PostAsJsonAsync($"/api/routes/{code}/parcels/{cash.Id}/confirm", new { statusId = "4" })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var delivered = await courierClient.PostAsJsonAsync($"/api/routes/{code}/parcels/{cash.Id}/confirm", new { statusId = 4 });
        delivered.StatusCode.ShouldBe(HttpStatusCode.Created);
        var line = (await delivered.JsonAsync()).GetProperty("parcels").EnumerateArray().Single(p => p.GetProperty("id").GetString() == cash.Id);
        line.GetProperty("confirmed").GetBoolean().ShouldBeTrue();

        await factory.ExecuteDbAsync(async db =>
        {
            var link = await db.RouteParcels.SingleAsync(l => l.ParcelId == cash.Id);
            link.CodCollected.ShouldBe(150.5m);
            link.CodCash.ShouldBe(150.5m);
            link.CourierCommission.ShouldBe(15m);
            var remittance = await db.CashRemittances.Include(r => r.Parcels).SingleAsync(r => r.CourierId == courier.Id);
            remittance.Status.ShouldBe(CashRemittanceStatus.DRAFT);
            remittance.Origin.ShouldBe(RemittanceOrigin.AUTOMATIC);
            remittance.ExpectedCod.ShouldBe(150.5m);
            remittance.ExpectedCommission.ShouldBe(15m);
            remittance.ExpectedNet.ShouldBe(135.5m);
            remittance.Parcels.Single().ParcelId.ShouldBe(cash.Id);
            var parcel = await db.Parcels.SingleAsync(p => p.Id == cash.Id);
            parcel.FinancialStatus.ShouldBe(FinancialStatus.ENCAISSE);
            parcel.CourierCommission.ShouldBe(15m);
        });

        var outcome = await world.Admin.PostAsJsonAsync($"/api/routes/{code}/outcomes", new { parcelIds = new[] { refused.Id }, statusId = 8 });
        outcome.StatusCode.ShouldBe(HttpStatusCode.Created);
        var result = await outcome.JsonAsync();
        result.GetProperty("count").GetInt32().ShouldBe(1);
        result.GetProperty("cashExpected").GetInt64().ShouldBe(0);
        (await (await world.Admin.GetAsync($"/api/routes/{code}")).JsonAsync()).GetProperty("status").GetString().ShouldBe("dispatched");
        (await world.Admin.PostAsJsonAsync($"/api/routes/{code}/outcomes", new { parcelIds = Array.Empty<string>(), statusId = 8 })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var closed = await world.Admin.PostAsJsonAsync($"/api/routes/{code}/close", new { });
        closed.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await closed.JsonAsync()).GetProperty("status").GetString().ShouldBe("closed");
        (await courierClient.PostAsJsonAsync($"/api/routes/{code}/parcels/{refused.Id}/confirm", new { statusId = 4 })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await (await world.Admin.PostAsync("/api/routes/auto-close-sweep", null)).JsonAsync()).GetArrayLength().ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Simultaneous_deliveries_of_one_courier_open_a_single_remittance()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var (courier, courierClient) = await CourierAsync(factory, world.City.Id);
        var parcels = new List<(string Id, string Code)>();
        for (var i = 0; i < 4; i++)
        {
            var parcel = await DeliveryNotesContractTests.ParcelAsync(world, cod: 100 + i);
            await ReadyAsync(factory, parcel.Id);
            parcels.Add(parcel);
        }
        var code = (await (await world.Admin.PostAsJsonAsync("/api/routes", new { cityId = world.City.Id, courierId = courier.Id })).JsonAsync()).GetProperty("code").GetString()!;
        (await world.Admin.PostAsJsonAsync($"/api/routes/{code}/parcels", new { parcelIds = parcels.Select(p => p.Id).ToArray() })).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await world.Admin.PostAsync($"/api/routes/{code}/dispatch", null)).StatusCode.ShouldBe(HttpStatusCode.Created);

        var answers = await Task.WhenAll(parcels.Select(p => courierClient.PostAsJsonAsync($"/api/routes/{code}/parcels/{p.Id}/confirm", new { statusId = 4 })));
        answers.Select(a => a.StatusCode).ShouldAllBe(s => s == HttpStatusCode.Created);

        await factory.ExecuteDbAsync(async db =>
        {
            var remittances = await db.CashRemittances.Include(r => r.Parcels).Where(r => r.CourierId == courier.Id).ToListAsync();
            remittances.Count.ShouldBe(1);
            remittances[0].Code.ShouldStartWith("RF-");
            remittances[0].Parcels.Count.ShouldBe(4);
            remittances[0].ExpectedCod.ShouldBe(406m);

            // Le filet : un second RF- ouvert pour ce livreur est refusé par l'index partiel.
            var remittance = remittances[0];
            db.CashRemittances.Add(new CashRemittance
            {
                Code = "RF-TEST-" + Guid.NewGuid().ToString("N")[..6], Status = CashRemittanceStatus.DRAFT, Origin = RemittanceOrigin.MANUAL, CityId = remittance.CityId, CourierId = courier.Id,
                AgencyId = remittance.AgencyId,
            });
            await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    private sealed record Money(decimal? LinkCollected, decimal? LinkCommission, decimal? ParcelCommission, FinancialStatus Financial, int StatusId, decimal RemittanceCod,
        decimal RemittanceCommission, decimal RemittanceNet, int RemittanceLines);

    private async Task<Money> MoneyOfAsync(string parcelId, string courierId)
    {
        Money? money = null;
        await factory.ExecuteDbAsync(async db =>
        {
            var link = await db.RouteParcels.AsNoTracking().SingleAsync(l => l.ParcelId == parcelId);
            var parcel = await db.Parcels.AsNoTracking().SingleAsync(p => p.Id == parcelId);
            var remittance = await db.CashRemittances.AsNoTracking().Include(r => r.Parcels).SingleAsync(r => r.CourierId == courierId);
            money = new Money(link.CodCollected, link.CourierCommission, parcel.CourierCommission, parcel.FinancialStatus, parcel.StatusId, remittance.ExpectedCod, remittance.ExpectedCommission,
                remittance.ExpectedNet, remittance.Parcels.Count);
        });
        return money!;
    }

    private async Task<(DeliveryNotesContractTests.World World, Courier Courier, HttpClient CourierClient, string Code, (string Id, string Code) Kept, (string Id, string Code) Undone)>
        TwoDeliveredAsync()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var (courier, courierClient) = await CourierAsync(factory, world.City.Id);
        await factory.ExecuteDbAsync(async db =>
        {
            db.CourierCities.Add(new CourierCity { CourierId = courier.Id, CityId = world.City.Id, DeliveryFee = 15m });
            await db.SaveChangesAsync();
        });
        var kept = await DeliveryNotesContractTests.ParcelAsync(world, cod: 80);
        var undone = await DeliveryNotesContractTests.ParcelAsync(world, cod: 150.5);
        await ReadyAsync(factory, kept.Id);
        await ReadyAsync(factory, undone.Id);
        var code = (await (await world.Admin.PostAsJsonAsync("/api/routes", new { cityId = world.City.Id, courierId = courier.Id })).JsonAsync()).GetProperty("code").GetString()!;
        await world.Admin.PostAsJsonAsync($"/api/routes/{code}/parcels", new { parcelIds = new[] { kept.Id, undone.Id } });
        await world.Admin.PostAsync($"/api/routes/{code}/dispatch", null);
        (await courierClient.PostAsJsonAsync($"/api/routes/{code}/parcels/{kept.Id}/confirm", new { statusId = 4 })).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await courierClient.PostAsJsonAsync($"/api/routes/{code}/parcels/{undone.Id}/confirm", new { statusId = 4 })).StatusCode.ShouldBe(HttpStatusCode.Created);
        return (world, courier, courierClient, code, kept, undone);
    }

    [Fact]
    public async Task Cancelling_a_delivery_undoes_cash_commission_and_remittance_line_with_an_audit_entry()
    {
        var (world, courier, courierClient, code, _, undone) = await TwoDeliveredAsync();
        var before = await MoneyOfAsync(undone.Id, courier.Id);
        before.ShouldBe(new Money(150.5m, 15m, 15m, FinancialStatus.ENCAISSE, 4, 230.5m, 30m, 200.5m, 2));
        (await (await world.Admin.GetAsync($"/api/routes/{code}")).JsonAsync()).GetProperty("status").GetString().ShouldBe("closed");

        (await courierClient.PostAsJsonAsync($"/api/parcels/{undone.Code}/cancel-delivery", new { reason = "Erreur" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await world.Admin.PostAsJsonAsync($"/api/parcels/{undone.Code}/cancel-delivery", new { reason = "" })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var cancelled = await world.Admin.PostAsJsonAsync($"/api/parcels/{undone.Code}/cancel-delivery", new { reason = "Client injoignable, livré par erreur" });
        cancelled.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await cancelled.JsonAsync();
        result.GetProperty("statusId").GetInt32().ShouldBe(13);
        result.GetProperty("routeCode").GetString().ShouldBe(code);
        result.GetProperty("routeReopened").GetBoolean().ShouldBeTrue();
        result.GetProperty("remittanceCode").GetString()!.ShouldStartWith("RF-");

        var after = await MoneyOfAsync(undone.Id, courier.Id);
        after.ShouldBe(new Money(null, null, null, FinancialStatus.NON_ENCAISSE, 13, 80m, 15m, 65m, 1));
        (await (await world.Admin.GetAsync($"/api/routes/{code}")).JsonAsync()).GetProperty("status").GetString().ShouldBe("dispatched");
        await factory.ExecuteDbAsync(async db =>
        {
            var audit = await db.AuditLogs.AsNoTracking().Where(a => a.Entity == "Parcel" && a.EntityId == undone.Id).OrderByDescending(a => a.At).FirstAsync();
            System.Text.Json.JsonDocument.Parse(audit.After!).RootElement.GetProperty("deliveryCancelled").GetBoolean().ShouldBeTrue();
            (await db.AuditLogs.AsNoTracking().Where(a => a.Entity == "Route").Select(a => a.After).ToListAsync()).ShouldContain(a => a!.Contains("reopened"));
        });
        (await world.Admin.PostAsJsonAsync($"/api/parcels/{undone.Code}/cancel-delivery", new { reason = "Encore" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Correcting_a_delivered_parcel_undoes_its_money_and_records_the_new_outcome_in_one_go()
    {
        var (world, courier, _, code, _, undone) = await TwoDeliveredAsync();
        var preview = await world.Admin.PostAsJsonAsync($"/api/parcels/{undone.Code}/correction/preview", new { statusId = 8, reason = "Refusé en réalité" });
        preview.StatusCode.ShouldBe(HttpStatusCode.OK);
        var plan = await preview.JsonAsync();
        plan.GetProperty("from").GetInt32().ShouldBe(4);
        plan.GetProperty("steps")[0].GetProperty("kind").GetString().ShouldBe("cancel-delivery");
        plan.GetProperty("routeCode").GetString().ShouldBe(code);
        var fingerprint = plan.GetProperty("fingerprint").GetString();

        (await world.Admin.PostAsJsonAsync($"/api/parcels/{undone.Code}/correction", new { statusId = 8, reason = "Refusé en réalité", fingerprint = "stale" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var applied = await world.Admin.PostAsJsonAsync($"/api/parcels/{undone.Code}/correction", new { statusId = 8, reason = "Refusé en réalité", fingerprint });
        applied.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await applied.JsonAsync()).GetProperty("to").GetInt32().ShouldBe(8);

        var after = await MoneyOfAsync(undone.Id, courier.Id);
        after.StatusId.ShouldBe(8);
        after.LinkCollected.ShouldBeNull();
        after.Financial.ShouldBe(FinancialStatus.NON_ENCAISSE);
        after.RemittanceCod.ShouldBe(80m);
        after.RemittanceLines.ShouldBe(1);
        await factory.ExecuteDbAsync(async db =>
        {
            var link = await db.RouteParcels.AsNoTracking().SingleAsync(l => l.ParcelId == undone.Id);
            link.Confirmed.ShouldBeTrue();
            link.FinalStatusId.ShouldBe(8);
            // Refusé : la course passe de la commission de livraison (grille) à celle d'échec (réglage).
            link.CourierCommissionSource.ShouldBe(CourierCommissionSource.FAILURE_SETTING);
            (await db.Parcels.AsNoTracking().SingleAsync(p => p.Id == undone.Id)).CourierCommissionSource.ShouldBe(CourierCommissionSource.FAILURE_SETTING);
            (await db.AuditLogs.AsNoTracking().Where(a => a.Entity == "Parcel" && a.EntityId == undone.Id).Select(a => a.After).ToListAsync()).ShouldContain(a => a!.Contains("statusCorrection"));
        });
    }

    [Fact]
    public async Task Draft_route_is_created_filled_emptied_and_deleted_within_courier_scope()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var (courier, courierClient) = await CourierAsync(factory, world.City.Id);
        var parcel = await DeliveryNotesContractTests.ParcelAsync(world);
        await ReadyAsync(factory, parcel.Id);

        (await world.Admin.PostAsJsonAsync("/api/routes", new { courierId = courier.Id })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var created = await world.Admin.PostAsJsonAsync("/api/routes", new { cityId = world.City.Id, courierId = courier.Id });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var route = await created.JsonAsync();
        var code = route.GetProperty("code").GetString()!;
        code.ShouldStartWith("CIR-");
        route.GetProperty("status").GetString().ShouldBe("draft");
        route.GetProperty("kind").GetString().ShouldBe("delivery");

        var candidates = await (await world.Admin.GetAsync($"/api/routes/{code}/candidates")).JsonAsync();
        candidates.EnumerateArray().Select(c => c.GetProperty("id").GetString()).ShouldContain(parcel.Id);
        var page = await (await world.Admin.GetAsync($"/api/routes/{code}/candidates?page=1&pageSize=5")).JsonAsync();
        page.GetProperty("total").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
        page.GetProperty("cities").GetArrayLength().ShouldBeGreaterThanOrEqualTo(1);
        (await (await world.Admin.GetAsync($"/api/routes/{code}/note-candidates")).JsonAsync()).GetArrayLength().ShouldBe(0);

        var added = await world.Admin.PostAsJsonAsync($"/api/routes/{code}/parcels", new { parcelIds = new[] { parcel.Id } });
        added.StatusCode.ShouldBe(HttpStatusCode.Created);
        var detail = await added.JsonAsync();
        detail.GetProperty("parcels").GetArrayLength().ShouldBe(1);
        detail.GetProperty("parcels")[0].GetProperty("code").GetString().ShouldBe(parcel.Code);

        (await world.Admin.PatchAsJsonAsync($"/api/routes/{code}", new { kind = "pickup" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await world.Admin.PatchAsJsonAsync($"/api/routes/{code}", new { })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await world.Admin.DeleteAsync($"/api/routes/{code}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await courierClient.GetAsync($"/api/routes/{code}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        var list = await (await courierClient.GetAsync("/api/routes")).JsonAsync();
        list.GetProperty("total").GetInt32().ShouldBe(1);
        var stats = await (await world.Admin.GetAsync($"/api/routes/stats?courierId={courier.Id}")).JsonAsync();
        stats.GetProperty("total").GetInt32().ShouldBe(1);
        stats.GetProperty("parcels").GetInt32().ShouldBe(1);
        (await world.Admin.GetAsync("/api/routes/export")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await courierClient.GetAsync($"/api/routes/{code}/export")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var (_, stranger) = await CourierAsync(factory, world.City.Id);
        (await stranger.GetAsync($"/api/routes/{code}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await (await stranger.GetAsync("/api/routes")).JsonAsync()).GetProperty("total").GetInt32().ShouldBe(0);

        var removed = await world.Admin.DeleteAsync($"/api/routes/{code}/parcels/{parcel.Id}");
        removed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await removed.JsonAsync()).GetProperty("parcels").GetArrayLength().ShouldBe(0);

        var patched = await world.Admin.PatchAsJsonAsync($"/api/routes/{code}", new { kind = "pickup" });
        patched.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await patched.JsonAsync()).GetProperty("kind").GetString().ShouldBe("pickup");

        var deleted = await world.Admin.DeleteAsync($"/api/routes/{code}");
        deleted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await deleted.JsonAsync()).GetProperty("code").GetString().ShouldBe(code);
        (await world.Admin.GetAsync($"/api/routes/{code}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
