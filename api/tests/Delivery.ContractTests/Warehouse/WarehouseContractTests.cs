using System.Net;
using System.Net.Http.Json;
using Delivery.Application.Common.Interfaces;
using Delivery.ContractTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public async Task Orders_become_parcels_and_take_stock_out()
    {
        var (_, city) = await TestData.CityAsync(factory);
        var agency = await TestData.AgencyAsync(factory, city.Id);
        var merchant = await TestData.MerchantAsync(factory, city.Id);
        var seller = await TestData.ClientForAsync(factory, new User { Id = merchant.UserId!, Email = merchant.Email });
        var admin = await TestData.ClientForAsync(factory, await TestData.UserAsync(factory, RoleName.SUPER_ADMIN));
        var excel = factory.Services.GetRequiredService<IExcelService>();
        string Workbook(string[] header, params object?[][] rows) =>
            Convert.ToBase64String(excel.Write("Feuille", header, rows.Select(r => (IReadOnlyList<object?>)r).ToList(), null));

        var catalog = Workbook(["Référence", "Nom", "Prix", "Poids"], ["SKU-9", "Sac", "120,5", "300"], ["", "Sans réf", "", ""], ["SKU-9", "Doublon", "", ""]);
        var preview = await (await seller.PostAsJsonAsync("/api/products/import", new { content = catalog })).JsonAsync();
        preview.GetProperty("errors").GetArrayLength().ShouldBe(2);
        preview.GetProperty("created").GetInt32().ShouldBe(0);
        var applied = await (await seller.PostAsJsonAsync("/api/products/import", new { content = Workbook(["Référence", "Nom", "Prix"], ["SKU-9", "Sac", "120,5"]), apply = true })).JsonAsync();
        applied.GetProperty("created").GetInt32().ShouldBe(1);
        applied.GetProperty("sample")[0].GetProperty("price").GetInt64().ShouldBe(12050);
        var productId = (await (await seller.GetAsync("/api/products?search=SKU-9")).JsonAsync()).GetProperty("items")[0].GetProperty("id").GetString();

        var storage = await (await seller.PostAsJsonAsync("/api/storage-requests", new { clientId = merchant.Id, agencyId = agency.Id, items = new[] { new { productId, quantity = 5 } } })).JsonAsync();
        await admin.PostAsJsonAsync($"/api/storage-requests/{storage.GetProperty("code").GetString()}/decision", new { approve = true });

        var created = await seller.PostAsJsonAsync("/api/orders", new
        {
            clientId = merchant.Id, agencyId = agency.Id, externalRef = "WEB-1", recipientName = "Nadia", recipientPhone = "06-11-22-33-44",
            destinationCityId = city.Id, deliveryAddress = "Bd Zerktouni", lines = new[] { new { productId, quantity = 2, unitPrice = (double?)null } },
        });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var order = await created.JsonAsync();
        var code = order.GetProperty("code").GetString()!;
        code.ShouldStartWith("CMD-");
        order.GetProperty("status").GetString().ShouldBe("draft");
        order.GetProperty("recipientPhone").GetString().ShouldBe("0611223344");
        order.GetProperty("totalAmount").GetInt64().ShouldBe(24100);
        order.GetProperty("parcelCreatable").GetBoolean().ShouldBeFalse();
        (await seller.PostAsJsonAsync($"/api/orders/{code}/parcel", new { })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await (await seller.PostAsJsonAsync($"/api/orders/{code}/confirm", new { })).JsonAsync()).GetProperty("status").GetString().ShouldBe("confirmed");
        var converted = await (await seller.PostAsJsonAsync($"/api/orders/{code}/parcel", new { fragile = true })).JsonAsync();
        converted.GetProperty("status").GetString().ShouldBe("parcel-created");
        var parcelCode = converted.GetProperty("parcelCode").GetString();
        var parcel = await (await seller.GetAsync($"/api/parcels/{parcelCode}")).JsonAsync();
        parcel.GetProperty("codAmount").GetInt64().ShouldBe(24100);
        parcel.GetProperty("externalCode").GetString().ShouldBe("WEB-1");
        parcel.GetProperty("items")[0].GetProperty("quantity").GetInt32().ShouldBe(2);
        (await (await seller.GetAsync("/api/stock")).JsonAsync()).GetProperty("items")[0].GetProperty("quantity").GetInt32().ShouldBe(3);
        (await seller.PostAsJsonAsync($"/api/orders/{code}/cancel", new { })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await seller.DeleteAsync($"/api/orders/{code}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await seller.PostAsJsonAsync("/api/orders", new
        {
            clientId = merchant.Id, externalRef = "WEB-1", recipientName = "X", recipientPhone = "0600000000", destinationCityId = city.Id, deliveryAddress = "Y",
            lines = new[] { new { productId, quantity = 1 } },
        })).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var orders = Workbook(["Référence", "Destinataire", "Téléphone", "Ville", "Adresse", "Produit", "Quantité"],
            ["WEB-1", "Déjà", "0600000000", city.Name, "x", "SKU-9", "1"],
            ["WEB-2", "Karim", "0600000001", city.Name, "Rue 2", "SKU-9", "4"],
            ["WEB-3", "Leila", "0600000002", "Nulle part", "Rue 3", "SKU-9", ""]);
        var ordersPreview = await (await seller.PostAsJsonAsync("/api/orders/import", new { content = orders })).JsonAsync();
        ordersPreview.GetProperty("skipped").GetInt32().ShouldBe(1);
        ordersPreview.GetProperty("valid").GetInt32().ShouldBe(1);
        ordersPreview.GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("unknown-city");
        var imported = await (await seller.PostAsJsonAsync("/api/orders/import", new { content = Workbook(["Référence", "Destinataire", "Téléphone", "Ville", "Adresse", "Produit", "Quantité"], ["WEB-2", "Karim", "0600000001", city.Name, "Rue 2", "SKU-9", "4"]), apply = true })).JsonAsync();
        imported.GetProperty("created").GetInt32().ShouldBe(1);
        var list = await (await seller.GetAsync("/api/orders?source=excel")).JsonAsync();
        list.GetProperty("total").GetInt32().ShouldBe(1);
        list.GetProperty("items")[0].GetProperty("source").GetString().ShouldBe("excel");
    }
}
