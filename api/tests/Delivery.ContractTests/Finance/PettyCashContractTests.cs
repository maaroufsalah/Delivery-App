using System.Net;
using System.Net.Http.Json;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.PettyCash;
using Delivery.ContractTests.Infrastructure;
using Delivery.ContractTests.Routes;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.ContractTests.Finance;

/// <summary>`PettyCashController` et `PettyCashCourierController` : mouvements, soldes chaînés, journées closes.</summary>
[Collection(ApiCollection.Name)]
public sealed class PettyCashContractTests(ApiFactory factory)
{
    private string Today => factory.Services.GetRequiredService<IClock>().Today();

    [Fact]
    public async Task Movements_chain_the_balance_and_a_closed_day_refuses_writes_until_reopened()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var admin = world.Admin;
        var adminId = (await (await admin.GetAsync("/api/auth/me")).JsonAsync()).GetProperty("id").GetString()!;
        var yesterday = DateOnly.ParseExact(Today, "yyyy-MM-dd").AddDays(-1).ToString("yyyy-MM-dd");

        var supply = await admin.PostAsJsonAsync("/api/petty-cash", new { kind = "supply", date = yesterday, amount = 500, agencyId = world.Agency.Id, note = "Fond de caisse" });
        supply.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await supply.JsonAsync()).GetProperty("amount").GetInt64().ShouldBe(50000);
        var missing = await admin.PostAsJsonAsync("/api/petty-cash", new { kind = "expense", date = Today, amount = 20, agencyId = world.Agency.Id });
        missing.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await admin.PostAsJsonAsync("/api/petty-cash", new { kind = "expense", date = Today, amount = 0, agencyId = world.Agency.Id, label = "x", category = "Transport", buyerId = adminId }))
            .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await admin.PostAsJsonAsync("/api/petty-cash", new { kind = "expense", date = Today, amount = 20, agencyId = world.Agency.Id, label = "Taxi", category = "Inconnue", buyerId = adminId }))
            .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var expense = await (await admin.PostAsJsonAsync("/api/petty-cash",
            new { kind = "expense", date = Today, amount = 120.5, agencyId = world.Agency.Id, label = "Taxi", category = "Transport", buyerId = adminId })).JsonAsync();
        expense.GetProperty("buyerName").GetString().ShouldNotBeNullOrEmpty();
        expense.GetProperty("dayStatus").GetString().ShouldBe("open");

        var page = await (await admin.GetAsync($"/api/petty-cash?agencyId={world.Agency.Id}&period=all")).JsonAsync();
        page.GetProperty("total").GetInt32().ShouldBe(2);
        page.GetProperty("totals").GetProperty("balance").GetInt64().ShouldBe(50000 - 12050);
        page.GetProperty("items")[0].GetProperty("date").GetString().ShouldBe(Today);

        var day = await (await admin.GetAsync($"/api/petty-cash/days/{Today}?agencyId={world.Agency.Id}")).JsonAsync();
        day.GetProperty("summary").GetProperty("openingBalance").GetInt64().ShouldBe(50000);
        day.GetProperty("summary").GetProperty("closingBalance").GetInt64().ShouldBe(50000 - 12050);

        // La veille se clôt (balayage), puis refuse toute écriture tant qu'on ne la rouvre pas.
        await factory.ExecuteDbAsync(async db =>
        {
            using var scope = factory.Services.CreateScope();
            await PettyCashDays.SweepAsync(db, scope.ServiceProvider.GetRequiredService<IClock>(), CancellationToken.None);
        });
        var closedDay = await (await admin.GetAsync($"/api/petty-cash/days/{yesterday}?agencyId={world.Agency.Id}")).JsonAsync();
        closedDay.GetProperty("summary").GetProperty("status").GetString().ShouldBe("closed");
        closedDay.GetProperty("summary").GetProperty("closedBy").GetString().ShouldBe("automatic");
        var refused = await admin.PostAsJsonAsync("/api/petty-cash", new { kind = "supply", date = yesterday, amount = 10, agencyId = world.Agency.Id });
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await refused.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("petty-cash-day-closed");
        (await admin.PostAsJsonAsync($"/api/petty-cash/days/{yesterday}/reopen", new { agencyId = world.Agency.Id })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var reopened = await admin.PostAsJsonAsync($"/api/petty-cash/days/{yesterday}/reopen", new { agencyId = world.Agency.Id, reason = "Oubli" });
        reopened.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await reopened.JsonAsync()).GetProperty("summary").GetProperty("reopenReason").GetString().ShouldBe("Oubli");
        (await admin.PostAsJsonAsync("/api/petty-cash", new { kind = "supply", date = yesterday, amount = 10, agencyId = world.Agency.Id })).StatusCode.ShouldBe(HttpStatusCode.Created);
        var today = await (await admin.GetAsync($"/api/petty-cash/days/{Today}?agencyId={world.Agency.Id}")).JsonAsync();
        today.GetProperty("summary").GetProperty("openingBalance").GetInt64().ShouldBe(51000);
        (await (await admin.PostAsJsonAsync($"/api/petty-cash/days/{Today}/close", new { agencyId = world.Agency.Id })).JsonAsync())
            .GetProperty("summary").GetProperty("status").GetString().ShouldBe("closed");

        var days = await (await admin.GetAsync($"/api/petty-cash/days?agencyId={world.Agency.Id}")).JsonAsync();
        days.GetProperty("items").GetArrayLength().ShouldBe(2);
        days.GetProperty("today").GetString().ShouldBe(Today);
        (await admin.GetAsync($"/api/petty-cash/days/{Today}/pdf?agencyId={world.Agency.Id}")).Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        (await admin.GetAsync($"/api/petty-cash/pdf?agencyId={world.Agency.Id}&period=all")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admin.GetAsync($"/api/petty-cash/days/{Today}/export?agencyId={world.Agency.Id}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await world.Seller.GetAsync("/api/petty-cash")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Courier_expense_stays_a_draft_out_of_the_balance_until_validated()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var (courier, phone) = await RoutesContractTests.CourierAsync(factory, world.City.Id);
        await factory.ExecuteDbAsync(async db =>
        {
            db.Couriers.Attach(courier);
            courier.AgencyId = world.Agency.Id;
            await db.SaveChangesAsync();
        });
        var created = await phone.PostAsJsonAsync("/api/courier/me/petty-cash", new { label = "Carburant", category = "Transport", amount = 50 });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var expense = await created.JsonAsync();
        var id = expense.GetProperty("id").GetString()!;
        expense.GetProperty("status").GetString().ShouldBe("draft");
        expense.GetProperty("agencyId").GetString().ShouldBe(world.Agency.Id);

        var mine = await (await phone.GetAsync("/api/courier/me/petty-cash")).JsonAsync();
        mine.GetProperty("drafts").GetInt32().ShouldBe(1);
        mine.GetProperty("categories").GetArrayLength().ShouldBeGreaterThan(0);
        var list = await (await world.Admin.GetAsync($"/api/petty-cash?agencyId={world.Agency.Id}&period=all")).JsonAsync();
        list.GetProperty("totals").GetProperty("drafts").GetInt32().ShouldBe(1);
        list.GetProperty("totals").GetProperty("balance").GetInt64().ShouldBe(0);
        var updated = await (await phone.PatchAsJsonAsync($"/api/courier/me/petty-cash/{id}", new { label = "Carburant", category = "Transport", amount = 60 })).JsonAsync();
        updated.GetProperty("amount").GetInt64().ShouldBe(6000);

        var validated = await (await world.Admin.PatchAsync($"/api/petty-cash/{id}/validate", null)).JsonAsync();
        validated.GetProperty("status").GetString().ShouldBe("validated");
        (await world.Admin.PatchAsync($"/api/petty-cash/{id}/validate", null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await phone.PatchAsJsonAsync($"/api/courier/me/petty-cash/{id}", new { label = "x", category = "Transport", amount = 1 })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await phone.DeleteAsync($"/api/courier/me/petty-cash/{id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var after = await (await world.Admin.GetAsync($"/api/petty-cash?agencyId={world.Agency.Id}&period=all")).JsonAsync();
        after.GetProperty("totals").GetProperty("balance").GetInt64().ShouldBe(-6000);
        var stored = await factory.QueryDbAsync(db => db.PettyCashDays.Where(d => d.AgencyId == world.Agency.Id).Select(d => new { d.Spent, d.PendingDrafts }).SingleAsync());
        stored.Spent.ShouldBe(60m);
        stored.PendingDrafts.ShouldBe(0);

        var second = await (await phone.PostAsJsonAsync("/api/courier/me/petty-cash", new { label = "Péage", category = "Transport", amount = 10 })).JsonAsync();
        var refused = await (await world.Admin.PatchAsJsonAsync($"/api/petty-cash/{second.GetProperty("id").GetString()}/refuse", new { reason = "Sans reçu" })).JsonAsync();
        refused.GetProperty("status").GetString().ShouldBe("refused");
        refused.GetProperty("refusalReason").GetString().ShouldBe("Sans reçu");
        var third = (await (await phone.PostAsJsonAsync("/api/courier/me/petty-cash", new { label = "Eau", category = "Autre", amount = 5 })).JsonAsync()).GetProperty("id").GetString();
        (await phone.DeleteAsync($"/api/courier/me/petty-cash/{third}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await phone.GetAsync("/api/courier/me/petty-cash/export")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await phone.GetAsync("/api/petty-cash")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        _ = PettyCashStatus.DRAFT;
    }
}
