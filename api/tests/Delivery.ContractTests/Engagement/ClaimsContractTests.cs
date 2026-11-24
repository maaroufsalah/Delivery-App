using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Delivery.ContractTests.Infrastructure;
using Delivery.ContractTests.Routes;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.ContractTests.Engagement;

/// <summary>`ClaimsController` : du type de réclamation au fil traité, assigné et clos.</summary>
[Collection(ApiCollection.Name)]
public sealed class ClaimsContractTests(ApiFactory factory)
{
    internal static readonly object Pdf = new { filename = "preuve.pdf", mimeType = "application/pdf", content = Convert.ToBase64String("%PDF-1.4 test"u8.ToArray()) };

    internal static async Task<string> TypeAsync(DeliveryNotesContractTests.World world, string subjectType = "parcel")
    {
        var type = await world.Admin.PostAsJsonAsync("/api/claims/types", new { code = TestData.Unique("type-").ToLowerInvariant(), label = "Colis endommagé", subjectType, sortOrder = "3" });
        type.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await type.JsonAsync()).GetProperty("id").GetString()!;
    }

    internal static async Task<JsonElement> OpenAsync(DeliveryNotesContractTests.World world, string typeId, string parcelCode)
    {
        var created = await world.Seller.PostAsJsonAsync("/api/claims", new { typeId, subject = "Carton abîmé", message = "Le carton est arrivé ouvert.", parcelCode, attachments = new[] { Pdf } });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await created.JsonAsync();
    }

    [Fact]
    public async Task Claim_types_are_unique_and_kept_while_used()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var code = TestData.Unique("casse-").ToLowerInvariant();
        var created = await (await world.Admin.PostAsJsonAsync("/api/claims/types", new { code, label = "Casse" })).JsonAsync();
        created.GetProperty("subjectType").GetString().ShouldBe("parcel");
        created.GetProperty("sortOrder").GetInt32().ShouldBe(0);
        created.GetProperty("isActive").GetBoolean().ShouldBeTrue();
        var id = created.GetProperty("id").GetString()!;
        var twice = await world.Admin.PostAsJsonAsync("/api/claims/types", new { code, label = "Casse" });
        twice.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await twice.JsonAsync()).GetProperty("details").GetProperty("field").GetString().ShouldBe("code");
        (await world.Admin.PostAsJsonAsync("/api/claims/types", new { code = "AB C", label = "Casse" })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await world.Admin.PostAsJsonAsync("/api/claims/types", new { code = "ok-code", label = "Casse", sortOrder = 1000 })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await world.Seller.PostAsJsonAsync("/api/claims/types", new { code = "x-y", label = "Casse" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var updated = await (await world.Admin.PatchAsJsonAsync($"/api/claims/types/{id}", new { code, label = "Casse totale", subjectType = "general", isActive = false })).JsonAsync();
        updated.GetProperty("label").GetString().ShouldBe("Casse totale");
        updated.GetProperty("subjectType").GetString().ShouldBe("general");
        (await (await world.Seller.GetAsync("/api/claims/types")).JsonAsync()).EnumerateArray().Select(t => t.GetProperty("id").GetString()).ShouldContain(id);

        var parcel = await DeliveryNotesContractTests.ParcelAsync(world);
        var used = await TypeAsync(world);
        await OpenAsync(world, used, parcel.Code);
        var refused = await world.Admin.DeleteAsync($"/api/claims/types/{used}");
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await refused.JsonAsync()).GetProperty("details").GetProperty("references")[0].GetProperty("count").GetInt32().ShouldBe(1);

        (await world.Admin.DeleteAsync($"/api/claims/types/{id}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await (await world.Admin.GetAsync("/api/claims/types")).JsonAsync()).EnumerateArray().Select(t => t.GetProperty("id").GetString()).ShouldNotContain(id);
        var revived = await world.Admin.PostAsJsonAsync("/api/claims/types", new { code, label = "Casse revenue" });
        (await revived.JsonAsync()).GetProperty("id").GetString().ShouldBe(id);
    }

    [Fact]
    public async Task Claim_is_opened_answered_assigned_and_resolved()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var typeId = await TypeAsync(world);
        var parcel = await DeliveryNotesContractTests.ParcelAsync(world);

        var missing = await world.Seller.PostAsJsonAsync("/api/claims", new { typeId, subject = "Carton", message = "Abîmé" });
        missing.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await missing.JsonAsync()).GetProperty("details").GetProperty("field").GetString().ShouldBe("parcelCode");
        (await world.Seller.PostAsJsonAsync("/api/claims", new { typeId, subject = "Carton", message = "Abîmé", parcelCode = "INCONNU" })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await world.Admin.PostAsJsonAsync("/api/claims", new { typeId, subject = "Carton", message = "Abîmé", parcelCode = parcel.Code })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await world.Seller.PostAsJsonAsync("/api/claims", new { typeId, subject = "", message = "Abîmé", parcelCode = parcel.Code })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var claim = await OpenAsync(world, typeId, parcel.Code);
        var code = claim.GetProperty("code").GetString()!;
        code.ShouldStartWith("RCL-");
        claim.GetProperty("status").GetString().ShouldBe("open");
        claim.GetProperty("parcelCode").GetString().ShouldBe(parcel.Code);
        claim.GetProperty("clientName").GetString().ShouldBe(world.Merchant.Name);
        claim.GetProperty("allowedTransitions").GetArrayLength().ShouldBe(0);
        claim.GetProperty("events").GetArrayLength().ShouldBe(1);
        claim.GetProperty("events")[0].GetProperty("status").GetString().ShouldBe("open");
        var attachment = claim.GetProperty("attachments")[0];
        attachment.GetProperty("mimeType").GetString().ShouldBe("application/pdf");
        var opened = await factory.QueryDbAsync(db => db.Notifications.Where(n => n.Topic == "claim-opened").Select(n => n.Params).ToListAsync());
        opened.Count(p => p.Contains(code)).ShouldBeGreaterThan(0);

        var file = await world.Seller.GetAsync($"/api/claims/{code}/attachments/{attachment.GetProperty("id").GetString()}");
        file.StatusCode.ShouldBe(HttpStatusCode.OK);
        file.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        file.Content.Headers.ContentDisposition!.FileName!.Trim('"').ShouldBe("preuve.pdf");

        var staff = await (await world.Admin.GetAsync($"/api/claims/{code}")).JsonAsync();
        staff.GetProperty("allowedTransitions").EnumerateArray().Select(s => s.GetString()).ShouldBe(["in-progress", "resolved", "rejected"]);

        (await world.Seller.PostAsJsonAsync($"/api/claims/{code}/events", new { })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await world.Seller.PostAsJsonAsync($"/api/claims/{code}/events", new { status = "resolved" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await world.Seller.PostAsJsonAsync($"/api/claims/{code}/events", new { body = "Note", isInternal = true })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var note = await world.Admin.PostAsJsonAsync($"/api/claims/{code}/events", new { body = "Vérifier avec le livreur", isInternal = true });
        note.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await (await world.Seller.GetAsync($"/api/claims/{code}")).JsonAsync()).GetProperty("events").GetArrayLength().ShouldBe(1);
        (await factory.QueryDbAsync(db => db.Notifications.CountAsync(n => n.UserId == world.Merchant.UserId && n.Topic == "claim-answered"))).ShouldBe(0);

        var agent = await TestData.UserAsync(factory, RoleName.STAFF);
        var assigned = await (await world.Admin.PatchAsJsonAsync($"/api/claims/{code}/assignee", new { assigneeId = agent.Id })).JsonAsync();
        assigned.GetProperty("assigneeId").GetString().ShouldBe(agent.Id);
        assigned.GetProperty("assigneeName").GetString().ShouldBe($"{agent.FirstName} {agent.LastName}");
        assigned.GetProperty("events").EnumerateArray().Last().GetProperty("body").GetString().ShouldBe($"Assignée à {agent.FirstName} {agent.LastName}.");
        (await world.Admin.PatchAsJsonAsync($"/api/claims/{code}/assignee", new { })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await world.Admin.PatchAsJsonAsync($"/api/claims/{code}/assignee", new { assigneeId = "inconnu" })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await (await world.Admin.PatchAsJsonAsync($"/api/claims/{code}/assignee", new { assigneeId = (string?)null })).JsonAsync()).GetProperty("assigneeId").ValueKind
            .ShouldBe(JsonValueKind.Null);
        (await world.Seller.PatchAsJsonAsync($"/api/claims/{code}/assignee", new { assigneeId = agent.Id })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var resolved = await (await world.Admin.PostAsJsonAsync($"/api/claims/{code}/events", new { body = "Remboursé", status = "resolved", attachments = new[] { Pdf } })).JsonAsync();
        resolved.GetProperty("status").GetString().ShouldBe("resolved");
        resolved.GetProperty("resolvedAt").ValueKind.ShouldBe(JsonValueKind.String);
        resolved.GetProperty("allowedTransitions").EnumerateArray().Select(s => s.GetString()).ShouldBe(["in-progress"]);
        resolved.GetProperty("events").EnumerateArray().Last().GetProperty("attachments").GetArrayLength().ShouldBe(1);
        (await factory.QueryDbAsync(db => db.Notifications.CountAsync(n => n.UserId == world.Merchant.UserId && n.Topic == "claim-answered"))).ShouldBe(1);
        var forbidden = await world.Admin.PostAsJsonAsync($"/api/claims/{code}/events", new { status = "open" });
        forbidden.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await forbidden.JsonAsync()).GetProperty("details").GetProperty("to").GetString().ShouldBe("open");
        var reopened = await (await world.Admin.PostAsJsonAsync($"/api/claims/{code}/events", new { status = "in-progress" })).JsonAsync();
        reopened.GetProperty("resolvedAt").ValueKind.ShouldBe(JsonValueKind.Null);

        var mine = await (await world.Seller.GetAsync("/api/claims?status=in-progress,open")).JsonAsync();
        mine.GetProperty("total").GetInt32().ShouldBe(1);
        mine.GetProperty("items")[0].GetProperty("code").GetString().ShouldBe(code);
        (await world.Seller.GetAsync("/api/claims?status=bogus")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await (await world.Seller.GetAsync("/api/claims/counters")).JsonAsync()).GetProperty("inProgress").GetInt32().ShouldBe(1);
        var found = await (await world.Admin.GetAsync($"/api/claims?search={code}")).JsonAsync();
        found.GetProperty("total").GetInt32().ShouldBe(1);
        var export = await world.Admin.GetAsync($"/api/claims/export?search={code}");
        export.StatusCode.ShouldBe(HttpStatusCode.OK);
        export.Content.Headers.ContentDisposition!.FileName!.Trim('"').ShouldStartWith("Reclamations-");
    }

    [Fact]
    public async Task Courier_opens_a_claim_only_on_a_parcel_entrusted_to_him()
    {
        var world = await DeliveryNotesContractTests.WorldAsync(factory);
        var (courier, phone) = await RoutesContractTests.CourierAsync(factory, world.City.Id);
        var typeId = await TypeAsync(world);
        var parcel = await DeliveryNotesContractTests.ParcelAsync(world);
        (await phone.PostAsJsonAsync("/api/claims", new { typeId, subject = "Adresse", message = "Introuvable", parcelCode = parcel.Code })).StatusCode
            .ShouldBe(HttpStatusCode.UnprocessableEntity);
        await factory.ExecuteDbAsync(async db =>
        {
            (await db.Parcels.SingleAsync(p => p.Id == parcel.Id)).CourierId = courier.Id;
            await db.SaveChangesAsync();
        });
        var created = await phone.PostAsJsonAsync("/api/claims", new { typeId, subject = "Adresse", message = "Introuvable", parcelCode = parcel.Code });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var claim = await created.JsonAsync();
        claim.GetProperty("clientId").GetString().ShouldBe(world.Merchant.Id);
        (await (await phone.GetAsync("/api/claims")).JsonAsync()).GetProperty("total").GetInt32().ShouldBe(1);
        (await world.Seller.GetAsync($"/api/claims/{claim.GetProperty("code").GetString()}")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
