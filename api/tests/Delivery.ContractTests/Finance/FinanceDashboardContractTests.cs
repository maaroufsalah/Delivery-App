using System.Net;
using System.Net.Http.Json;
using Delivery.ContractTests.Infrastructure;
using Delivery.Domain.Entities;

namespace Delivery.ContractTests.Finance;

/// <summary>`FinanceDashboardController` et `BanksController`.</summary>
[Collection(ApiCollection.Name)]
public sealed class FinanceDashboardContractTests(ApiFactory factory)
{
    [Fact]
    public async Task Dashboard_counts_the_cash_still_held_by_the_courier()
    {
        var (world, courier, phone, _) = await RemittancesContractTests.DeliveredAsync(factory);
        var held = (await (await phone.GetAsync("/api/courier/me/remittance")).JsonAsync()).GetProperty("remittance").GetProperty("expectedCod").GetInt64();

        var dashboard = await (await world.Admin.GetAsync("/api/finance/dashboard")).JsonAsync();
        var row = dashboard.GetProperty("cashToRemit").EnumerateArray().Single(r => r.GetProperty("courierId").GetString() == courier.Id);
        row.GetProperty("amount").GetInt64().ShouldBe(held);
        row.GetProperty("parcelCount").GetInt32().ShouldBe(1);
        dashboard.GetProperty("unpaidInvoices").GetProperty("count").GetInt32().ShouldBeGreaterThanOrEqualTo(0);
        (await world.Seller.GetAsync("/api/finance/dashboard")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var overview = await (await world.Admin.GetAsync($"/api/finance/overview?period=all&courierId={courier.Id}")).JsonAsync();
        overview.GetProperty("courierId").GetString().ShouldBe(courier.Id);
        overview.GetProperty("cash").GetProperty("collected").GetInt64().ShouldBe(held);
        overview.GetProperty("cash").GetProperty("openCount").GetInt32().ShouldBe(1);
        overview.GetProperty("deliveredCount").GetInt32().ShouldBe(1);
        (await world.Admin.GetAsync("/api/finance/overview?period=decade")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var mine = await (await phone.GetAsync("/api/finance/overview?period=all")).JsonAsync();
        mine.GetProperty("courierId").GetString().ShouldBe(courier.Id);
    }

    [Fact]
    public async Task Banks_are_listed_and_the_company_accounts_stay_off_the_merchant_and_courier()
    {
        var world = await Routes.DeliveryNotesContractTests.WorldAsync(factory);
        var bank = new Bank { Name = TestData.Unique("Banque ") };
        await factory.ExecuteDbAsync(async db =>
        {
            db.Banks.Add(bank);
            db.BankAccounts.Add(new BankAccount { BankId = bank.Id, Label = TestData.Unique("Compte "), Rib = "011780000012345678901234" });
            await db.SaveChangesAsync();
        });
        (await (await world.Seller.GetAsync("/api/banks")).JsonAsync()).EnumerateArray().Select(b => b.GetProperty("id").GetString()).ShouldContain(bank.Id);
        (await world.Seller.GetAsync("/api/bank-accounts")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var accounts = await (await world.Admin.GetAsync("/api/bank-accounts")).JsonAsync();
        accounts.EnumerateArray().Single(a => a.GetProperty("bankId").GetString() == bank.Id).GetProperty("bankName").GetString().ShouldBe(bank.Name);
        var (_, phone) = await Routes.RoutesContractTests.CourierAsync(factory, world.City.Id);
        (await (await phone.GetAsync("/api/banks")).JsonAsync()).GetArrayLength().ShouldBe(0);
    }
}
