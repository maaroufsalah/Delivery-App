using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Delivery.ContractTests.Infrastructure;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.ContractTests.Accounts;

[Collection(ApiCollection.Name)]
public sealed class AccountsContractTests(ApiFactory factory)
{
    private async Task<HttpClient> AdminAsync() => await TestData.ClientForAsync(factory, await TestData.UserAsync(factory, RoleName.SUPER_ADMIN));

    [Fact]
    public async Task Client_card_is_created_patched_and_detailed()
    {
        var admin = await AdminAsync();
        var (zone, city) = await TestData.CityAsync(factory);
        var created = await admin.PostAsJsonAsync("/api/clients", new { name = "Boutique Atlas", phone = "06 12 34 56 78", email = "atlas@shop.ma", cityId = city.Id, taxId = "" });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var client = await created.JsonAsync();
        client.GetProperty("type").GetString().ShouldBe("merchant");
        client.GetProperty("billingType").GetString().ShouldBe("normal");
        client.GetProperty("phone").GetString().ShouldBe("0612345678");
        client.GetProperty("taxId").ValueKind.ShouldBe(JsonValueKind.Null);
        client.GetProperty("agencyId").GetString().ShouldNotBeNull();
        client.GetProperty("validatedAt").GetString().ShouldNotBeNull();
        client.GetProperty("discounts").GetArrayLength().ShouldBe(0);
        var id = client.GetProperty("id").GetString();

        var patched = await (await admin.PatchAsJsonAsync($"/api/clients/{id}", new { isPro = true, billingType = "professional", notes = (string?)null })).JsonAsync();
        patched.GetProperty("isPro").GetBoolean().ShouldBeTrue();
        patched.GetProperty("billingType").GetString().ShouldBe("professional");
        patched.GetProperty("name").GetString().ShouldBe("Boutique Atlas");

        var badTax = await admin.PatchAsJsonAsync($"/api/clients/{id}", new { taxId = "123" });
        badTax.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var vat = await (await admin.PatchAsJsonAsync($"/api/clients/{id}/vat-rate", new { vatRate = 1000 })).JsonAsync();
        vat.GetProperty("vatRate").GetInt32().ShouldBe(1000);
        (await admin.PatchAsJsonAsync($"/api/clients/{id}/vat-rate", new { vatRate = 1500 })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var terms = await (await admin.PatchAsJsonAsync($"/api/clients/{id}/pickup-terms", new { pickupFee = 1500, freePickupThreshold = (int?)null })).JsonAsync();
        terms.GetProperty("pickupFee").GetInt64().ShouldBe(1500);

        var discount = await admin.PostAsJsonAsync($"/api/clients/{id}/discounts", new { zoneIds = new[] { zone.Id }, flatRate = 18 });
        discount.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await discount.JsonAsync()).GetProperty("discounts")[0].GetProperty("flatRate").GetInt64().ShouldBe(1800);
        var duplicate = await admin.PostAsJsonAsync($"/api/clients/{id}/discounts", new { zoneIds = new[] { zone.Id }, flatRate = 15 });
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await duplicate.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("discount-duplicate");

        var address = await (await admin.PostAsJsonAsync($"/api/clients/{id}/pickup-addresses", new { label = "Dépôt", address = "12 rue Atlas", cityId = city.Id, isDefault = true })).JsonAsync();
        address.GetProperty("pickupAddresses")[0].GetProperty("isDefault").GetBoolean().ShouldBeTrue();

        var document = await (await admin.PostAsJsonAsync($"/api/clients/{id}/documents", new { name = "rib.pdf", kind = "rib", mimeType = "application/pdf", content = Convert.ToBase64String("%PDF-1.4 test"u8.ToArray()) })).JsonAsync();
        var doc = document.GetProperty("documents")[0];
        doc.GetProperty("kind").GetString().ShouldBe("rib");
        var file = await admin.GetAsync($"/api/clients/{id}/documents/{doc.GetProperty("id").GetString()}");
        file.StatusCode.ShouldBe(HttpStatusCode.OK);
        file.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");

        var list = await (await admin.GetAsync("/api/clients?search=atlas")).JsonAsync();
        list.GetProperty("items").EnumerateArray().ShouldContain(c => c.GetProperty("id").GetString() == id);
    }

    [Fact]
    public async Task Registration_is_validated_by_an_admin_then_the_courier_logs_in()
    {
        var admin = await AdminAsync();
        var (_, city) = await TestData.CityAsync(factory);
        var agency = await TestData.AgencyAsync(factory, city.Id);
        var username = TestData.Unique("liv");
        (await factory.CreateClient().PostAsJsonAsync("/api/public/register", new
        {
            kind = "COURIER", firstName = "Omar", lastName = "Idrissi", phone = "0611111111", cityId = city.Id, username, password = "Courier-123",
        })).StatusCode.ShouldBe(HttpStatusCode.Created);

        var pending = await (await admin.GetAsync("/api/directory/pending-counts")).JsonAsync();
        pending.GetProperty("couriers").GetInt32().ShouldBeGreaterThan(0);
        var courierId = await factory.QueryDbAsync(db => db.Couriers.Where(c => c.User!.Username == username).Select(c => c.Id).FirstAsync());

        var validated = await admin.PostAsJsonAsync($"/api/couriers/{courierId}/validate", new { agencyId = agency.Id });
        validated.StatusCode.ShouldBe(HttpStatusCode.Created);
        var card = await validated.JsonAsync();
        card.GetProperty("isActive").GetBoolean().ShouldBeTrue();
        card.GetProperty("agencyId").GetString().ShouldBe(agency.Id);
        card.GetProperty("payModel").GetString().ShouldBe("commission");
        card.GetProperty("settlementMode").GetString().ShouldBe("at-remittance");

        var login = await factory.CreateMobileClient().PostAsJsonAsync("/api/auth/login", new { identifier = username, password = "Courier-123" });
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await login.JsonAsync()).GetProperty("courierId").GetString().ShouldBe(courierId);
    }

    [Fact]
    public async Task Courier_pay_grid_is_replaced_and_audited()
    {
        var admin = await AdminAsync();
        var (_, city) = await TestData.CityAsync(factory);
        var created = await (await admin.PostAsJsonAsync("/api/couriers", new { firstName = "Yassine", lastName = "Amrani", phone = "0622222222", cityId = city.Id, fixedSalary = 2500 })).JsonAsync();
        created.GetProperty("fixedSalary").GetInt64().ShouldBe(250000);
        var id = created.GetProperty("id").GetString();

        var grid = await (await admin.PutAsJsonAsync($"/api/couriers/{id}/cities", new { cities = new[] { new { cityId = city.Id, pickupFee = 5, deliveryFee = 12.5 } } })).JsonAsync();
        grid.GetProperty("cities")[0].GetProperty("deliveryFee").GetInt64().ShouldBe(1250);
        var twice = await admin.PutAsJsonAsync($"/api/couriers/{id}/cities", new { cities = new[] { new { cityId = city.Id, pickupFee = 5, deliveryFee = 1 }, new { cityId = city.Id, pickupFee = 5, deliveryFee = 1 } } });
        twice.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var audit = await factory.QueryDbAsync(db => db.AuditLogs.CountAsync(a => a.Entity == "Courier" && a.EntityId == id && a.Diff.Contains("cities")));
        audit.ShouldBe(1);

        var account = await admin.PostAsJsonAsync($"/api/couriers/{id}/account", new { email = TestData.Unique("yas") + "@wimo.test", firstName = "Yassine", lastName = "Amrani", roles = new[] { "STAFF" }, password = "Password-1" });
        account.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await account.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("fiche-role");
    }

    [Fact]
    public async Task Merchant_reads_and_updates_its_own_card()
    {
        var (_, city) = await TestData.CityAsync(factory);
        var merchant = await TestData.MerchantAsync(factory, city.Id);
        var user = await factory.QueryDbAsync(db => db.Users.SingleAsync(u => u.Id == merchant.UserId));
        var client = await TestData.ClientForAsync(factory, user);

        var company = await (await client.GetAsync("/api/merchant/me/company")).JsonAsync();
        company.GetProperty("pickupTerms").GetProperty("fee").GetInt64().ShouldBe(2000);
        company.GetProperty("pickupTerms").GetProperty("freeFrom").GetInt64().ShouldBe(5);

        var updated = await (await client.PatchAsJsonAsync("/api/merchant/me/company", new { name = "Nouvelle boutique", phone = "0699999999", cityId = city.Id })).JsonAsync();
        updated.GetProperty("name").GetString().ShouldBe("Nouvelle boutique");
    }

    [Fact]
    public async Task Scope_sweep_agents_merchants_and_couriers_stay_in_their_lane()
    {
        var (_, city) = await TestData.CityAsync(factory);
        var mine = await TestData.AgencyAsync(factory, city.Id);
        var other = await TestData.AgencyAsync(factory, city.Id);
        var inMine = await TestData.MerchantAsync(factory, city.Id);
        var inOther = await TestData.MerchantAsync(factory, city.Id);
        await factory.ExecuteDbAsync(async db =>
        {
            await db.Clients.Where(c => c.Id == inMine.Id).ExecuteUpdateAsync(c => c.SetProperty(x => x.AgencyId, mine.Id));
            await db.Clients.Where(c => c.Id == inOther.Id).ExecuteUpdateAsync(c => c.SetProperty(x => x.AgencyId, other.Id));
        });
        var agent = await TestData.ClientForAsync(factory, await TestData.UserAsync(factory, RoleName.STAFF, mine.Id));

        var visible = await (await agent.GetAsync("/api/clients?pageSize=200")).JsonAsync();
        var ids = visible.GetProperty("items").EnumerateArray().Select(c => c.GetProperty("id").GetString()).ToList();
        ids.ShouldContain(inMine.Id);
        ids.ShouldNotContain(inOther.Id);
        (await agent.GetAsync($"/api/clients/{inOther.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var merchantUser = await factory.QueryDbAsync(db => db.Users.SingleAsync(u => u.Id == inMine.UserId));
        var merchant = await TestData.ClientForAsync(factory, merchantUser);
        var refused = await merchant.GetAsync("/api/clients");
        refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await refused.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("merchant-cannot-do-this");
        (await merchant.GetAsync("/api/couriers")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var staff = await TestData.ClientForAsync(factory, await TestData.UserAsync(factory, RoleName.STAFF));
        var notMerchant = await staff.GetAsync("/api/merchant/me/company");
        notMerchant.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await notMerchant.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("not-a-merchant");
    }
}
