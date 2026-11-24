using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Delivery.ContractTests.Infrastructure;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;

namespace Delivery.ContractTests.Parcels;

[Collection(ApiCollection.Name)]
public sealed class ParcelsContractTests(ApiFactory factory)
{
    private sealed record World(City City, Agency Agency, Client Merchant, HttpClient MerchantHttp, HttpClient Admin);

    private async Task<World> WorldAsync()
    {
        var (_, city) = await TestData.CityAsync(factory);
        var agency = await TestData.AgencyAsync(factory, city.Id);
        var merchant = await TestData.MerchantAsync(factory, city.Id);
        var merchantUser = new User { Id = merchant.UserId!, Email = merchant.Email };
        var merchantHttp = await TestData.ClientForAsync(factory, merchantUser);
        var admin = await TestData.ClientForAsync(factory, await TestData.UserAsync(factory, RoleName.SUPER_ADMIN));
        return new World(city, agency, merchant, merchantHttp, admin);
    }

    private static object Body(World world, object? overrides = null) => new
    {
        clientId = world.Merchant.Id,
        description = "  Chaussures ",
        recipientName = "Salma",
        recipientPhone = "06 12 34 56 78 / 0700000000",
        originCityId = world.City.Id,
        pickupAddress = "12 rue Atlas",
        destinationCityId = world.City.Id,
        deliveryAddress = "Maarif",
        codAmount = 199.9,
    };

    [Fact]
    public async Task Merchant_creates_lists_and_reads_a_parcel()
    {
        var world = await WorldAsync();
        var created = await world.MerchantHttp.PostAsJsonAsync("/api/parcels", Body(world));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var parcel = await created.JsonAsync();
        var code = parcel.GetProperty("code").GetString()!;
        Regex.IsMatch(code, $"^{world.City.Code}\\d{{2}}001\\d{{4}}{world.City.Code}$").ShouldBeTrue(code);
        parcel.GetProperty("statusId").GetInt32().ShouldBe(1);
        parcel.GetProperty("description").GetString().ShouldBe("Chaussures");
        parcel.GetProperty("recipientPhone").GetString().ShouldBe("0612345678 / 0700000000");
        parcel.GetProperty("codAmount").GetInt64().ShouldBe(19990);
        parcel.GetProperty("deliveryFee").GetInt64().ShouldBe(2000);
        parcel.GetProperty("courierId").ValueKind.ShouldBe(JsonValueKind.Null);
        parcel.GetProperty("merchantFinancialStatus").GetString().ShouldBe("non-encaisse");
        parcel.TryGetProperty("financialStatus", out _).ShouldBeFalse();
        parcel.GetProperty("agencyId").GetString().ShouldBe(world.Agency.Id);

        var list = await (await world.MerchantHttp.GetAsync("/api/parcels")).JsonAsync();
        list.GetProperty("total").GetInt32().ShouldBe(1);
        list.GetProperty("items")[0].GetProperty("clientName").GetString().ShouldBe(world.Merchant.Name);

        var detail = await (await world.MerchantHttp.GetAsync($"/api/parcels/{code.ToLowerInvariant()}")).JsonAsync();
        detail.GetProperty("agencyName").GetString().ShouldBe(world.Agency.Name);
        detail.GetProperty("codToCollect").GetInt64().ShouldBe(19990);
        detail.GetProperty("events").GetArrayLength().ShouldBe(1);
        detail.GetProperty("events")[0].GetProperty("comment").ValueKind.ShouldBe(JsonValueKind.Null);
        detail.GetProperty("events")[0].GetProperty("blStep").ValueKind.ShouldBe(JsonValueKind.Null);
        detail.TryGetProperty("finance", out _).ShouldBeFalse();
        detail.GetProperty("routes").GetArrayLength().ShouldBe(0);

        var adminDetail = await (await world.Admin.GetAsync($"/api/parcels/{code}")).JsonAsync();
        adminDetail.GetProperty("finance").GetProperty("toCollect").GetInt64().ShouldBe(19990);
        adminDetail.GetProperty("financialStatus").GetString().ShouldBe("non-encaisse");
        adminDetail.GetProperty("events")[0].GetProperty("authorName").GetString()!.ShouldStartWith("Test ");

        var stats = await (await world.MerchantHttp.GetAsync("/api/parcels/stats")).JsonAsync();
        stats.GetProperty("total").GetInt32().ShouldBe(1);
        stats.GetProperty("deliveryRate").GetDouble().ShouldBe(0);
        stats.TryGetProperty("window", out _).ShouldBeFalse();

        var export = await world.MerchantHttp.GetAsync("/api/parcels/export");
        export.StatusCode.ShouldBe(HttpStatusCode.OK);
        export.Content.Headers.ContentType!.MediaType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        var next = await (await world.MerchantHttp.GetAsync($"/api/parcels/next-code?originCityId={world.City.Id}&destinationCityId={world.City.Id}")).JsonAsync();
        next.GetProperty("code").GetString()!.ShouldContain("002");

        var quote = await world.MerchantHttp.PostAsJsonAsync("/api/parcels/quote", new { originCityId = world.City.Id, destinationCityId = world.City.Id });
        quote.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await quote.JsonAsync()).GetProperty("sameCity").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Idempotency_key_replays_the_first_parcel()
    {
        var world = await WorldAsync();
        var key = TestData.Unique("idem-key-");
        HttpRequestMessage Request() => new(HttpMethod.Post, "/api/parcels") { Content = JsonContent.Create(Body(world)), Headers = { { "idempotency-key", key } } };
        var first = await (await world.MerchantHttp.SendAsync(Request())).JsonAsync();
        var second = await world.MerchantHttp.SendAsync(Request());
        second.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await second.JsonAsync()).GetProperty("id").GetString().ShouldBe(first.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Create_validates_like_zod_and_guards_the_sender()
    {
        var world = await WorldAsync();
        var invalid = await world.MerchantHttp.PostAsJsonAsync("/api/parcels", new { clientId = world.Merchant.Id, recipientPhone = "abc", codAmount = -1, quantity = "x" });
        invalid.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var issues = (await invalid.JsonAsync()).GetProperty("details").GetProperty("issues").EnumerateArray()
            .ToDictionary(i => i.GetProperty("path").GetString()!, i => i.GetProperty("message").GetString());
        issues["description"].ShouldBe("Invalid input: expected string, received undefined");
        issues["recipientPhone.0"].ShouldBe("invalid-phone");
        issues["codAmount"].ShouldBe("negative-amount");
        issues["quantity"].ShouldBe("required");

        var other = await TestData.MerchantAsync(factory, world.City.Id);
        var forbidden = await world.MerchantHttp.PostAsJsonAsync("/api/parcels", Body(world) is var body ? new
        {
            clientId = other.Id,
            description = "x",
            recipientName = "y",
            recipientPhone = "0600000000",
            originCityId = world.City.Id,
            pickupAddress = "a",
            destinationCityId = world.City.Id,
            deliveryAddress = "b",
            codAmount = 0,
        } : null);
        forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await world.MerchantHttp.GetAsync("/api/parcels/NOPE00000000NOP")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Scope_sweep_keeps_each_merchant_on_their_parcels()
    {
        var a = await WorldAsync();
        var b = await WorldAsync();
        var parcelA = await (await a.MerchantHttp.PostAsJsonAsync("/api/parcels", Body(a))).JsonAsync();
        await b.MerchantHttp.PostAsJsonAsync("/api/parcels", Body(b));
        var codeA = parcelA.GetProperty("code").GetString();

        (await b.MerchantHttp.GetAsync($"/api/parcels/{codeA}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var listB = await (await b.MerchantHttp.GetAsync($"/api/parcels?clientId={a.Merchant.Id}&pageSize=200")).JsonAsync();
        listB.GetProperty("items").EnumerateArray().Any(i => i.GetProperty("code").GetString() == codeA).ShouldBeFalse();

        var courier = await TestData.UserAsync(factory, RoleName.COURIER);
        var courierHttp = await TestData.ClientForAsync(factory, courier);
        var courierList = await courierHttp.GetAsync("/api/parcels");
        if (courierList.StatusCode == HttpStatusCode.OK) (await courierList.JsonAsync()).GetProperty("total").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Status_change_follows_transitions_and_guards()
    {
        var world = await WorldAsync();
        var code = (await (await world.MerchantHttp.PostAsJsonAsync("/api/parcels", Body(world))).JsonAsync()).GetProperty("code").GetString();

        (await world.MerchantHttp.PostAsJsonAsync($"/api/parcels/{code}/events", new { statusId = 3 })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var bad = await world.Admin.PostAsJsonAsync($"/api/parcels/{code}/events", new { statusId = 5 });
        bad.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await bad.JsonAsync()).GetProperty("code").GetString().ShouldBe("TRANSITION_FORBIDDEN");

        var delivered = await world.Admin.PostAsJsonAsync($"/api/parcels/{code}/events", new { statusId = 4 });
        delivered.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var moved = await world.Admin.PostAsJsonAsync($"/api/parcels/{code}/events", new { statusId = "3", comment = "  Ramassé " });
        moved.StatusCode.ShouldBe(HttpStatusCode.Created);
        var evt = await moved.JsonAsync();
        evt.GetProperty("statusId").GetInt32().ShouldBe(3);
        evt.GetProperty("comment").GetString().ShouldBe("Ramassé");
        evt.GetProperty("authorName").GetString()!.ShouldStartWith("Test ");

        var deliverDirect = await world.Admin.PostAsJsonAsync($"/api/parcels/{code}/events", new { statusId = 4 });
        deliverDirect.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await deliverDirect.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("deliver-through-route");

        var detail = await (await world.Admin.GetAsync($"/api/parcels/{code}")).JsonAsync();
        detail.GetProperty("statusId").GetInt32().ShouldBe(3);
        detail.GetProperty("events").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task Fees_notes_and_seller_edits_are_journaled()
    {
        var world = await WorldAsync();
        var code = (await (await world.MerchantHttp.PostAsJsonAsync("/api/parcels", Body(world))).JsonAsync()).GetProperty("code").GetString();

        var none = await world.Admin.PatchAsJsonAsync($"/api/parcels/{code}/fees", new { reason = "x" });
        none.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await none.JsonAsync()).GetProperty("details").GetProperty("issues")[0].GetProperty("message").GetString().ShouldBe("no-fee-changed");

        var fees = await (await world.Admin.PatchAsJsonAsync($"/api/parcels/{code}/fees", new { deliveryFee = 25, otherFees = 5, reason = " Hors gabarit " })).JsonAsync();
        fees.GetProperty("deliveryFee").GetInt64().ShouldBe(2500);
        fees.GetProperty("otherFees").GetInt64().ShouldBe(500);
        fees.GetProperty("feeChanges").GetArrayLength().ShouldBe(2);
        fees.GetProperty("feeChanges")[0].GetProperty("reason").GetString().ShouldBe("Hors gabarit");
        (await world.Admin.PatchAsJsonAsync($"/api/parcels/{code}/fees", new { deliveryFee = 25, reason = "x" })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var merchantView = await (await world.MerchantHttp.GetAsync($"/api/parcels/{code}")).JsonAsync();
        merchantView.GetProperty("feeChanges")[0].GetProperty("authorName").GetString().ShouldBe(string.Empty);
        merchantView.GetProperty("fieldChanges").GetArrayLength().ShouldBe(0);

        (await world.Admin.PatchAsJsonAsync($"/api/parcels/{code}/bl", new { blRequested = true })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await world.Admin.PatchAsJsonAsync($"/api/parcels/{code}/expected-payment", new { expectedPayment = "cheque", reason = "x" })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        (await world.MerchantHttp.PostAsJsonAsync($"/api/parcels/{code}/notes", new { body = "x" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var note = await world.Admin.PostAsJsonAsync($"/api/parcels/{code}/notes", new { body = "  Appeler avant  " });
        note.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await note.JsonAsync()).GetProperty("body").GetString().ShouldBe("Appeler avant");

        var updated = await world.MerchantHttp.PatchAsJsonAsync($"/api/parcels/{code}", new
        {
            description = "Sac", recipientName = "Salma", recipientPhone = "0612345678", destinationCityId = world.City.Id,
            deliveryAddress = "Gauthier", codAmount = 100, quantity = 2,
        });
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var after = await updated.JsonAsync();
        after.GetProperty("quantity").GetInt32().ShouldBe(2);
        after.GetProperty("deliveryFee").GetInt64().ShouldBe(4000);
        (await world.Admin.PatchAsJsonAsync($"/api/parcels/{code}", new { description = "x" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var removed = await world.MerchantHttp.DeleteAsync($"/api/parcels/{code}");
        removed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await removed.JsonAsync()).GetProperty("code").GetString().ShouldBe(code);
        (await world.MerchantHttp.GetAsync($"/api/parcels/{code}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var deleted = await world.Admin.GetAsync($"/api/parcels/{code}?deleted=true");
        deleted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await deleted.JsonAsync()).GetProperty("deletedAt").GetString().ShouldNotBeNull();
    }

    [Fact]
    public async Task Admin_changes_the_client_from_a_preview_then_deletes()
    {
        var world = await WorldAsync();
        var code = (await (await world.MerchantHttp.PostAsJsonAsync("/api/parcels", Body(world))).JsonAsync()).GetProperty("code").GetString();
        var other = await TestData.MerchantAsync(factory, world.City.Id);

        var staff = await TestData.ClientForAsync(factory, await TestData.UserAsync(factory, RoleName.STAFF, world.Agency.Id));
        var refused = await staff.PostAsJsonAsync($"/api/parcels/{code}/admin-delete", new { reason = "x" });
        if (refused.StatusCode == HttpStatusCode.Forbidden && (await refused.JsonAsync()).GetProperty("details").TryGetProperty("reason", out var reason))
        {
            reason.GetString().ShouldBe("admin-only");
        }

        var preview = await world.Admin.PostAsJsonAsync($"/api/parcels/{code}/client/preview", new { clientId = other.Id });
        preview.StatusCode.ShouldBe(HttpStatusCode.OK);
        var plan = await preview.JsonAsync();
        plan.GetProperty("from").GetProperty("id").GetString().ShouldBe(world.Merchant.Id);
        plan.GetProperty("changes")[0].GetProperty("field").GetString().ShouldBe("clientId");
        plan.GetProperty("beforePickup").GetBoolean().ShouldBeTrue();
        var fingerprint = plan.GetProperty("fingerprint").GetString();
        fingerprint!.Length.ShouldBe(16);

        (await world.Admin.PostAsJsonAsync($"/api/parcels/{code}/client", new { clientId = other.Id, reason = "Erreur", fingerprint = "0000000000000000" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var changed = await world.Admin.PostAsJsonAsync($"/api/parcels/{code}/client", new { clientId = other.Id, reason = "Erreur de saisie", fingerprint });
        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await changed.JsonAsync()).GetProperty("clientId").GetString().ShouldBe(other.Id);
        (await world.MerchantHttp.GetAsync($"/api/parcels/{code}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var deleted = await world.Admin.PostAsJsonAsync($"/api/parcels/{code}/admin-delete", new { reason = "Doublon" });
        deleted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await deleted.JsonAsync()).GetProperty("code").GetString().ShouldBe(code);
        var detail = await (await world.Admin.GetAsync($"/api/parcels/{code}?deleted=true")).JsonAsync();
        detail.GetProperty("fieldChanges").EnumerateArray().Select(c => c.GetProperty("field").GetString()).ShouldContain("deletedAt");
        detail.GetProperty("fieldChanges").EnumerateArray().First(c => c.GetProperty("field").GetString() == "clientId").GetProperty("newLabel").GetString().ShouldBe(other.Name);
    }

    [Fact]
    public async Task Destination_change_reprices_and_journals()
    {
        var world = await WorldAsync();
        var code = (await (await world.MerchantHttp.PostAsJsonAsync("/api/parcels", Body(world))).JsonAsync()).GetProperty("code").GetString();
        var (_, rabat) = await TestData.CityAsync(factory, "Rabat");

        var empty = await world.Admin.PatchAsJsonAsync($"/api/parcels/{code}/destination", new { reason = "x" });
        empty.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await empty.JsonAsync()).GetProperty("details").GetProperty("issues")[0].GetProperty("path").GetString().ShouldBe("deliveryAddress");

        var moved = await world.Admin.PatchAsJsonAsync($"/api/parcels/{code}/destination", new { destinationCityId = rabat.Id, destinationDistrictName = "Agdal", instructions = "Sonner", reason = "Client déménagé" });
        moved.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detail = await moved.JsonAsync();
        detail.GetProperty("destinationCityId").GetString().ShouldBe(rabat.Id);
        detail.GetProperty("destinationDistrictName").GetString().ShouldBe("Agdal");
        detail.GetProperty("repricedFrom").GetInt64().ShouldBe(2000);
        detail.GetProperty("repricedTo").GetInt64().ShouldBe(3000);
        detail.GetProperty("deliveryFee").GetInt64().ShouldBe(3000);
        detail.GetProperty("droppedFromRoutes").GetArrayLength().ShouldBe(0);
        detail.GetProperty("notes")[0].GetProperty("body").GetString().ShouldBe("Sonner");
        var fields = detail.GetProperty("fieldChanges").EnumerateArray().Select(c => c.GetProperty("field").GetString()).ToList();
        fields.ShouldContain("destinationCityId");
        fields.ShouldContain("deliveryFee");
        detail.GetProperty("fieldChanges").EnumerateArray().First(c => c.GetProperty("field").GetString() == "destinationCityId").GetProperty("newLabel").GetString().ShouldBe("Rabat");

        (await world.Admin.PatchAsJsonAsync($"/api/parcels/{code}/destination", new { destinationCityId = rabat.Id, reason = "x" })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await world.MerchantHttp.PatchAsJsonAsync($"/api/parcels/{code}/destination", new { deliveryAddress = "x", reason = "x" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Exchange_from_a_delivered_parcel_creates_return_and_exchange_parcels()
    {
        var world = await WorldAsync();
        var code = (await (await world.MerchantHttp.PostAsJsonAsync("/api/parcels", Body(world))).JsonAsync()).GetProperty("code").GetString()!;
        var early = await world.MerchantHttp.PostAsJsonAsync($"/api/parcels/{code}/exchange", new { returnTarget = "home", exchangePickup = "initial" });
        early.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await early.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("parcel-not-delivered");

        await factory.ExecuteDbAsync(async db =>
        {
            var parcel = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstAsync(db.Parcels, p => p.Code == code);
            parcel.StatusId = 4;
            await db.SaveChangesAsync();
        });
        var invalid = await world.MerchantHttp.PostAsJsonAsync($"/api/parcels/{code}/exchange", new { returnTarget = "agency", exchangePickup = "initial", alreadyPaid = true, price = 100 });
        invalid.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var created = await world.MerchantHttp.PostAsJsonAsync($"/api/parcels/{code}/exchange", new { returnTarget = "home", exchangePickup = "initial", price = 5000, newProduct = "Taille 42" });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var exchange = await created.JsonAsync();
        var exchangeCode = exchange.GetProperty("code").GetString()!;
        Regex.IsMatch(exchangeCode, @"^ECH-\d{4}-\d{4}$").ShouldBeTrue(exchangeCode);
        exchange.GetProperty("status").GetString().ShouldBe("nouveau-colis-cree");
        exchange.GetProperty("newParcelCode").GetString().ShouldBe($"ECH-{code}");
        exchange.GetProperty("amount").GetInt64().ShouldBe(5000);
        exchange.GetProperty("pickupable").GetBoolean().ShouldBeFalse();
        (await world.MerchantHttp.PostAsJsonAsync($"/api/parcels/{code}/exchange", new { returnTarget = "home", exchangePickup = "initial" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var ret = await (await world.MerchantHttp.GetAsync($"/api/parcels/RET-{code}")).JsonAsync();
        ret.GetProperty("kind").GetString().ShouldBe("return");
        ret.GetProperty("statusId").GetInt32().ShouldBe(20);
        var ech = await (await world.MerchantHttp.GetAsync($"/api/parcels/ECH-{code}")).JsonAsync();
        ech.GetProperty("kind").GetString().ShouldBe("exchange");
        ech.GetProperty("codAmount").GetInt64().ShouldBe(5000);
        ech.GetProperty("exchanges")[0].GetProperty("role").GetString().ShouldBe("new");

        (await world.Admin.PostAsJsonAsync($"/api/exchanges/{exchangeCode}/deliver", new { })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var picked = await (await world.Admin.PostAsJsonAsync($"/api/exchanges/{exchangeCode}/pickup", new { comment = "ok" })).JsonAsync();
        picked.GetProperty("status").GetString().ShouldBe("ancien-recupere");
        picked.GetProperty("deliverable").GetBoolean().ShouldBeTrue();
        await world.Admin.PostAsJsonAsync($"/api/exchanges/{exchangeCode}/deliver", new { });
        (await world.Admin.PostAsJsonAsync($"/api/exchanges/{exchangeCode}/cancel", new { })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var closed = await (await world.Admin.PostAsJsonAsync($"/api/exchanges/{exchangeCode}/close", new { })).JsonAsync();
        closed.GetProperty("status").GetString().ShouldBe("cloture");
        closed.GetProperty("events").GetArrayLength().ShouldBe(5);

        var list = await (await world.MerchantHttp.GetAsync("/api/exchanges?status=cloture")).JsonAsync();
        list.GetProperty("total").GetInt32().ShouldBe(1);
        list.GetProperty("pageSize").GetInt32().ShouldBe(20);
        (await world.MerchantHttp.PostAsJsonAsync("/api/exchanges", new { originalParcelCode = code, returnedProduct = "a", newProduct = "b" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Labels_render_as_pdf_now_or_through_a_job_and_tracking_is_public()
    {
        var world = await WorldAsync();
        var first = await (await world.MerchantHttp.PostAsJsonAsync("/api/parcels", Body(world))).JsonAsync();
        var code = first.GetProperty("code").GetString()!;
        var second = (await (await world.MerchantHttp.PostAsJsonAsync("/api/parcels", Body(world))).JsonAsync()).GetProperty("code").GetString()!;

        static async Task<byte[]> Pdf(HttpResponseMessage response)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
            var bytes = await response.Content.ReadAsByteArrayAsync();
            System.Text.Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
            return bytes;
        }

        await Pdf(await world.MerchantHttp.GetAsync($"/api/parcels/{code}/label"));
        await Pdf(await world.MerchantHttp.GetAsync($"/api/parcels/{code}/label?format=a4"));
        (await world.MerchantHttp.GetAsync($"/api/parcels/{code}/label?format=a5")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var batch = await world.MerchantHttp.GetAsync($"/api/parcels/labels?codes={code},{second}");
        await Pdf(batch);
        batch.Content.Headers.ContentDisposition!.FileName!.Trim('"').ShouldBe("etiquettes-2.pdf");
        (await world.MerchantHttp.GetAsync("/api/parcels/labels?codes=,")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await world.MerchantHttp.PostAsJsonAsync("/api/parcels/labels", new { })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await Pdf(await world.MerchantHttp.PostAsJsonAsync("/api/parcels/labels", new { all = true, exclude = new[] { second } }));

        await factory.ExecuteDbAsync(async db =>
        {
            db.Settings.Add(new Setting { Id = "cmp-wimo:company:labels.template", Scope = SettingScope.COMPANY, Key = "labels.template", Value = "\"branded\"" });
            await db.SaveChangesAsync();
        });
        try
        {
            await Pdf(await world.MerchantHttp.GetAsync($"/api/parcels/{code}/label"));
        }
        finally
        {
            await factory.ExecuteDbAsync(async db =>
            {
                db.Settings.Remove(await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstAsync(db.Settings, x => x.Id == "cmp-wimo:company:labels.template"));
                await db.SaveChangesAsync();
            });
        }

        var job = await world.MerchantHttp.PostAsJsonAsync("/api/parcels/label-jobs", new { codes = new[] { code, second }, format = "a4" });
        job.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var jobId = (await job.JsonAsync()).GetProperty("id").GetString();
        JsonElement state = default;
        for (var i = 0; i < 50; i++)
        {
            state = await (await world.MerchantHttp.GetAsync($"/api/parcels/label-jobs/{jobId}")).JsonAsync();
            if (state.GetProperty("status").GetString() is "ready" or "failed") break;
            await Task.Delay(200);
        }
        state.GetProperty("status").GetString().ShouldBe("ready");
        state.GetProperty("count").GetInt32().ShouldBe(2);
        state.GetProperty("fileType").GetString().ShouldBe("pdf");
        await Pdf(await world.MerchantHttp.GetAsync($"/api/parcels/label-jobs/{jobId}/file"));
        (await (await world.MerchantHttp.GetAsync("/api/parcels/label-jobs")).JsonAsync()).GetArrayLength().ShouldBe(1);
        (await world.Admin.GetAsync($"/api/parcels/label-jobs/{jobId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var anonymous = factory.CreateClient();
        var track = await anonymous.GetAsync($"/api/public/track/{code.ToLowerInvariant()}");
        track.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tracking = await track.JsonAsync();
        tracking.GetProperty("code").GetString().ShouldBe(code);
        tracking.GetProperty("group").GetString().ShouldBe("initial");
        tracking.GetProperty("steps").GetArrayLength().ShouldBe(1);
        (await anonymous.GetAsync("/api/public/track/NOPE")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Excel_import_previews_then_creates_the_valid_rows()
    {
        var world = await WorldAsync();
        var noAddress = await world.MerchantHttp.PostAsJsonAsync("/api/parcels/import/preview", new { rows = new[] { new { recipientName = "A" } } });
        noAddress.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await noAddress.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("missing-pickup-address");
        await factory.ExecuteDbAsync(async db =>
        {
            var client = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstAsync(db.Clients, c => c.Id == world.Merchant.Id);
            client.Address = "Zone industrielle, lot 4";
            await db.SaveChangesAsync();
        });

        var template = await world.MerchantHttp.GetAsync("/api/parcels/import/template.xlsx");
        template.StatusCode.ShouldBe(HttpStatusCode.OK);
        template.Content.Headers.ContentType!.MediaType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var workbook = Convert.ToBase64String(await template.Content.ReadAsByteArrayAsync());
        var fromFile = await (await world.MerchantHttp.PostAsJsonAsync("/api/parcels/import/preview", new { content = workbook })).JsonAsync();
        fromFile.GetProperty("rows").GetInt32().ShouldBe(1);

        (await world.MerchantHttp.PostAsJsonAsync("/api/parcels/import/preview", new { })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var rows = new object[]
        {
            new { externalCode = "CMD-1", recipientName = "Salma", recipientPhone = "06 12 34 56 78", city = world.City.Name, district = "Inconnu", address = "12 rue Atlas", cod = "199,90", description = "Sac", openOnDelivery = "O", fragile = "", notes = "Fragile", quantity = "2", row = 2 },
            new { externalCode = "CMD-1", recipientName = "Omar", recipientPhone = "abc", city = "Nulle part", district = "", address = "x", cod = "abc", description = "", openOnDelivery = "peut-être", fragile = "", notes = "", quantity = "0", row = 3 },
        };
        var preview = await world.MerchantHttp.PostAsJsonAsync("/api/parcels/import/preview", new { rows });
        preview.StatusCode.ShouldBe(HttpStatusCode.Created);
        var plan = await preview.JsonAsync();
        plan.GetProperty("valid").GetInt32().ShouldBe(1);
        plan.GetProperty("invalid").GetInt32().ShouldBe(1);
        plan.GetProperty("estimatedFees").GetInt64().ShouldBe(4000);
        var good = plan.GetProperty("items")[0];
        good.GetProperty("warnings")[0].GetProperty("code").GetString().ShouldBe("district-unknown");
        var bad = plan.GetProperty("items")[1].GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("code").GetString()).ToList();
        bad.ShouldBe(["duplicate-in-file", "invalid-phone", "unknown-city", "not-a-number", "required", "not-a-boolean", "not-an-integer"]);

        var anomalies = await world.MerchantHttp.PostAsJsonAsync("/api/parcels/import/anomalies.xlsx", new { rows });
        anomalies.StatusCode.ShouldBe(HttpStatusCode.OK);

        var refused = await world.MerchantHttp.PostAsJsonAsync("/api/parcels/import", new { rows });
        refused.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var done = await world.MerchantHttp.PostAsJsonAsync("/api/parcels/import", new { rows, mode = "valid-only", filename = "commandes.xlsx" });
        done.StatusCode.ShouldBe(HttpStatusCode.Created);
        var result = await done.JsonAsync();
        result.GetProperty("created").GetInt32().ShouldBe(1);
        result.GetProperty("skipped").GetInt32().ShouldBe(1);
        result.GetProperty("warnings").GetInt32().ShouldBe(1);
        var code = result.GetProperty("codes")[0].GetString();
        var parcel = await (await world.MerchantHttp.GetAsync($"/api/parcels/{code}")).JsonAsync();
        parcel.GetProperty("codAmount").GetInt64().ShouldBe(19990);
        parcel.GetProperty("quantity").GetInt32().ShouldBe(2);
        parcel.GetProperty("openOnDelivery").GetBoolean().ShouldBeTrue();
        parcel.GetProperty("pickupAddress").GetString().ShouldBe("Zone industrielle, lot 4");

        var history = await (await world.MerchantHttp.GetAsync("/api/parcels/imports")).JsonAsync();
        history.GetProperty("total").GetInt32().ShouldBe(1);
        history.GetProperty("items")[0].GetProperty("mode").GetString().ShouldBe("valid-only");
        var report = await world.MerchantHttp.GetAsync($"/api/parcels/imports/{result.GetProperty("importId").GetString()}/report.xlsx");
        report.StatusCode.ShouldBe(HttpStatusCode.OK);

        var again = await (await world.MerchantHttp.PostAsJsonAsync("/api/parcels/import/preview", new { rows = new[] { rows[0] } })).JsonAsync();
        again.GetProperty("items")[0].GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("duplicate-external-code");
    }

    [Fact]
    public async Task Merchant_summary_and_courier_profile()
    {
        var world = await WorldAsync();
        await world.MerchantHttp.PostAsJsonAsync("/api/parcels", Body(world));
        var summary = await world.MerchantHttp.GetAsync("/api/merchant/me/summary");
        summary.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await summary.JsonAsync();
        body.GetProperty("createdToday").GetInt32().ShouldBe(1);
        body.GetProperty("parcelsTotal").GetInt32().ShouldBe(1);
        body.GetProperty("parcels").GetProperty("awaiting").GetInt32().ShouldBe(1);
        body.GetProperty("parcels").GetProperty("cancelled").GetInt32().ShouldBe(0);
        body.GetProperty("billingType").GetString().ShouldBe("normal");
        body.GetProperty("activity").GetArrayLength().ShouldBe(1);
        body.GetProperty("codToReceive").GetInt64().ShouldBe(0);
        (await world.Admin.GetAsync("/api/merchant/me/summary")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var user = await TestData.UserAsync(factory, RoleName.COURIER);
        var courier = new Courier { UserId = user.Id, FirstName = "Hamid", LastName = "B", Phone = "0600000009", CityId = world.City.Id };
        await factory.ExecuteDbAsync(async db =>
        {
            db.Couriers.Add(courier);
            await db.SaveChangesAsync();
        });
        var courierHttp = await TestData.ClientForAsync(factory, user);
        var me = await (await courierHttp.GetAsync("/api/courier/me")).JsonAsync();
        me.GetProperty("id").GetString().ShouldBe(courier.Id);
        me.GetProperty("agencyId").GetString().ShouldBe(world.Agency.Id);
        me.GetProperty("agencyName").GetString().ShouldBe(world.Agency.Name);
        me.GetProperty("vehicleType").GetString().ShouldNotBeNullOrEmpty();
        (await world.MerchantHttp.GetAsync("/api/courier/me")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
