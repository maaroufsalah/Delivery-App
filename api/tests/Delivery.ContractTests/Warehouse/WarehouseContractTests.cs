using System.Net;
using System.Net.Http.Json;
using Delivery.ContractTests.Infrastructure;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;

namespace Delivery.ContractTests.Warehouse;

[Collection(ApiCollection.Name)]
public sealed class WarehouseContractTests(ApiFactory factory)
{
    [Fact]
    public async Task Products_enter_stock_through_a_validated_storage_request()
    {
        var (_, city) = await TestData.CityAsync(factory);
        var agency = await TestData.AgencyAsync(factory, city.Id);
        var merchant = await TestData.MerchantAsync(factory, city.Id);
        var seller = await TestData.ClientForAsync(factory, new User { Id = merchant.UserId!, Email = merchant.Email });
        var admin = await TestData.ClientForAsync(factory, await TestData.UserAsync(factory, RoleName.SUPER_ADMIN));

        var invalid = await seller.PostAsJsonAsync("/api/products", new { clientId = "x", name = "A", lengthMm = 1.5, price = -1 });
        invalid.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var issues = (await invalid.JsonAsync()).GetProperty("details").GetProperty("issues").EnumerateArray().ToDictionary(i => i.GetProperty("path").GetString()!, i => i.GetProperty("message").GetString());
        issues["name"].ShouldBe("too-short");
        issues["lengthMm"].ShouldBe("not-integer");
        issues["price"].ShouldBe("negative-amount");

        var attributes = await (await admin.PostAsJsonAsync("/api/products/attributes", new { name = TestData.Unique("Taille") })).JsonAsync();
        var attributeId = attributes.EnumerateArray().First().GetProperty("id").GetString();
        var values = await (await admin.PostAsJsonAsync("/api/products/attributes/values", new { attributeId, value = "42" })).JsonAsync();
        var valueId = values.EnumerateArray().First(a => a.GetProperty("id").GetString() == attributeId).GetProperty("values")[0].GetProperty("id").GetString();

        var created = await seller.PostAsJsonAsync("/api/products", new { clientId = "ignored", reference = "SKU-1", name = "Chaussure", weightG = 800, price = 199.5, attributeValueIds = new[] { valueId } });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var product = await created.JsonAsync();
        product.GetProperty("clientId").GetString().ShouldBe(merchant.Id);
        product.GetProperty("price").GetInt64().ShouldBe(19950);
        product.GetProperty("attributes")[0].GetProperty("value").GetString().ShouldBe("42");
        product.GetProperty("stockTotal").GetInt32().ShouldBe(0);
        var productId = product.GetProperty("id").GetString();
        (await seller.PostAsJsonAsync("/api/products", new { clientId = "x", reference = "SKU-1", name = "Autre" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var storage = await seller.PostAsJsonAsync("/api/storage-requests", new { clientId = merchant.Id, agencyId = agency.Id, items = new[] { new { productId, quantity = 12 } } });
        storage.StatusCode.ShouldBe(HttpStatusCode.Created);
        var request = await storage.JsonAsync();
        var code = request.GetProperty("code").GetString()!;
        code.ShouldStartWith("ENT-");
        request.GetProperty("decidable").GetBoolean().ShouldBeTrue();
        (await seller.PostAsJsonAsync($"/api/storage-requests/{code}/decision", new { approve = true })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await admin.PostAsJsonAsync($"/api/storage-requests/{code}/decision", new { approve = false })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var decided = await (await admin.PostAsJsonAsync($"/api/storage-requests/{code}/decision", new { approve = true })).JsonAsync();
        decided.GetProperty("status").GetString().ShouldBe("validated");
        (await admin.PostAsJsonAsync($"/api/storage-requests/{code}/decision", new { approve = true })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var receipt = await seller.GetAsync($"/api/storage-requests/{code}/receipt");
        receipt.StatusCode.ShouldBe(HttpStatusCode.OK);
        receipt.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");

        var stock = await (await seller.GetAsync("/api/stock")).JsonAsync();
        stock.GetProperty("total").GetInt32().ShouldBe(1);
        var line = stock.GetProperty("items")[0];
        line.GetProperty("quantity").GetInt32().ShouldBe(12);
        line.GetProperty("lowStock").GetBoolean().ShouldBeFalse();
        var stockId = line.GetProperty("id").GetString();
        (await seller.PostAsJsonAsync("/api/stock/adjust", new { productId, agencyId = agency.Id, quantity = -2, reason = "x" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var tooMuch = await admin.PostAsJsonAsync("/api/stock/adjust", new { productId, agencyId = agency.Id, quantity = -20, reason = "Casse" });
        tooMuch.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await tooMuch.JsonAsync()).GetProperty("details").GetProperty("reason").GetString().ShouldBe("insufficient-stock");
        var adjusted = await (await admin.PostAsJsonAsync("/api/stock/adjust", new { productId, agencyId = agency.Id, quantity = -8, reason = "Casse" })).JsonAsync();
        adjusted.GetProperty("quantity").GetInt32().ShouldBe(4);
        adjusted.GetProperty("lowStock").GetBoolean().ShouldBeTrue();
        adjusted.GetProperty("movements").GetArrayLength().ShouldBe(2);
        adjusted.GetProperty("movements")[1].GetProperty("storageRequestCode").GetString().ShouldBe(code);
        (await (await seller.GetAsync($"/api/stock/{stockId}")).JsonAsync()).GetProperty("movements")[0].GetProperty("kind").GetString().ShouldBe("adjust");

        (await seller.DeleteAsync($"/api/products/{productId}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var other = await TestData.MerchantAsync(factory, city.Id);
        var otherSeller = await TestData.ClientForAsync(factory, new User { Id = other.UserId!, Email = other.Email });
        (await otherSeller.GetAsync($"/api/products/{productId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await (await otherSeller.GetAsync("/api/stock")).JsonAsync()).GetProperty("total").GetInt32().ShouldBe(0);
        (await (await otherSeller.GetAsync("/api/storage-requests")).JsonAsync()).GetProperty("total").GetInt32().ShouldBe(0);
        (await seller.GetAsync("/api/products/export")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
