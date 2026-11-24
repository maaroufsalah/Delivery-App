using System.Net;
using System.Net.Http.Json;
using Delivery.Application.Common.Interfaces;
using Delivery.ContractTests.Infrastructure;
using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.ContractTests.Finance;

/// <summary>`CourierStatementsController` : la paie d'un livreur, de la course confirmée au chèque photographié.</summary>
[Collection(ApiCollection.Name)]
public sealed class CourierStatementsContractTests(ApiFactory factory)
{
    private static readonly object Proof = new { name = "cheque.jpg", mimeType = "image/jpeg", content = Convert.ToBase64String([0xFF, 0xD8, 0xFF, 0xE0]) };

    private string Today => factory.Services.GetRequiredService<IClock>().Today();

    [Fact]
    public async Task Statement_counts_each_course_once_and_is_paid_by_cheque()
    {
        var (world, courier, phone, parcel) = await RemittancesContractTests.DeliveredAsync(factory);
        var body = new { courierId = courier.Id, agencyId = world.Agency.Id, periodStart = Today, periodEnd = Today };
        (await world.Admin.PostAsJsonAsync("/api/courier-statements", body with { periodEnd = "2020-01-01" })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var created = await world.Admin.PostAsJsonAsync("/api/courier-statements", body);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var draft = await created.JsonAsync();
        var code = draft.GetProperty("code").GetString()!;
        code.ShouldStartWith("PAI");
        draft.GetProperty("lineCount").GetInt32().ShouldBe(1);
        var line = draft.GetProperty("lines")[0];
        line.GetProperty("kind").GetString().ShouldBe("delivery");
        line.GetProperty("parcelCode").GetString().ShouldBe(parcel.Code);
        line.GetProperty("deducted").GetBoolean().ShouldBeFalse();
        var amount = line.GetProperty("amount").GetInt64();
        amount.ShouldBe(1500);
        draft.GetProperty("commissionsTotal").GetInt64().ShouldBe(1500);
        draft.GetProperty("totalDue").GetInt64().ShouldBe(1500);

        // La caisse confirmée garde la commission : le brouillon recalculé la soustrait (règlement à la remise).
        var remittance = (await (await phone.GetAsync("/api/courier/me/remittance")).JsonAsync()).GetProperty("remittance").GetProperty("code").GetString()!;
        (await world.Admin.PostAsync($"/api/remittances/{remittance}/confirm", null)).StatusCode.ShouldBe(HttpStatusCode.Created);
        var recomputed = await (await world.Admin.PostAsync($"/api/courier-statements/{code}/recompute", null)).JsonAsync();
        recomputed.GetProperty("commissionsDeducted").GetInt64().ShouldBe(1500);
        recomputed.GetProperty("variableAmount").GetInt64().ShouldBe(0);
        recomputed.GetProperty("totalDue").GetInt64().ShouldBe(0);
        recomputed.GetProperty("lines")[0].GetProperty("deducted").GetBoolean().ShouldBeTrue();

        (await world.Admin.PostAsJsonAsync($"/api/courier-statements/{code}/pay", new { method = "cash" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var validated = await (await world.Admin.PostAsync($"/api/courier-statements/{code}/validate", null)).JsonAsync();
        validated.GetProperty("status").GetString().ShouldBe("validated");
        validated.GetProperty("validatable").GetBoolean().ShouldBeFalse();
        (await world.Admin.PostAsync($"/api/courier-statements/{code}/recompute", null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var second = await (await world.Admin.PostAsJsonAsync("/api/courier-statements", body)).JsonAsync();
        second.GetProperty("lineCount").GetInt32().ShouldBe(0);
        (await world.Admin.DeleteAsync($"/api/courier-statements/{second.GetProperty("code").GetString()}")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var bank = new Bank { Name = TestData.Unique("Banque ") };
        var account = new BankAccount { BankId = bank.Id, Label = TestData.Unique("Compte ") };
        await factory.ExecuteDbAsync(async db =>
        {
            db.Banks.Add(bank);
            db.BankAccounts.Add(account);
            await db.SaveChangesAsync();
        });
        var noNumber = await world.Admin.PostAsJsonAsync($"/api/courier-statements/{code}/pay", new { method = "check", bankAccountId = account.Id });
        noNumber.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await world.Admin.PostAsJsonAsync($"/api/courier-statements/{code}/pay", new { method = "transfer" })).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var paid = await (await world.Admin.PostAsJsonAsync($"/api/courier-statements/{code}/pay",
            new { method = "check", bankAccountId = account.Id, chequeNumber = "0042", chequeDate = Today })).JsonAsync();
        paid.GetProperty("status").GetString().ShouldBe("paid");
        paid.GetProperty("paymentMethod").GetString().ShouldBe("check");
        paid.GetProperty("bankAccountLabel").GetString().ShouldBe(account.Label);
        (await world.Admin.DeleteAsync($"/api/courier-statements/{code}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var cheques = await (await phone.GetAsync("/api/courier/me/cheques")).JsonAsync();
        cheques.GetArrayLength().ShouldBe(1);
        cheques[0].GetProperty("number").GetString().ShouldBe("0042");
        cheques[0].GetProperty("handedOver").GetBoolean().ShouldBeTrue();
        cheques[0].GetProperty("hasPhoto").GetBoolean().ShouldBeFalse();
        var deposited = await phone.PostAsJsonAsync("/api/courier/me/cheques", new { chequeNumber = "9999", proof = Proof });
        deposited.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detail = await deposited.JsonAsync();
        detail.GetProperty("chequeNumber").GetString().ShouldBe("0042");
        detail.GetProperty("proofName").GetString().ShouldBe("cheque.jpg");
        var photo = await world.Admin.GetAsync($"/api/courier-statements/{code}/cheque-photo");
        photo.StatusCode.ShouldBe(HttpStatusCode.OK);
        photo.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("inline");
        (await world.Admin.GetAsync($"/api/courier-statements/{code}/pdf")).Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        (await world.Seller.GetAsync("/api/courier-statements")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await (await phone.GetAsync("/api/courier-statements")).JsonAsync()).GetProperty("total").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Cancelled_delivery_leaves_the_draft_statement_recomputed()
    {
        var (world, courier, _, parcel) = await RemittancesContractTests.DeliveredAsync(factory);
        var draft = await (await world.Admin.PostAsJsonAsync("/api/courier-statements",
            new { courierId = courier.Id, agencyId = world.Agency.Id, periodStart = Today, periodEnd = Today })).JsonAsync();
        var code = draft.GetProperty("code").GetString()!;
        draft.GetProperty("totalDue").GetInt64().ShouldBe(1500);

        (await world.Admin.PostAsJsonAsync($"/api/parcels/{parcel.Code}/cancel-delivery", new { reason = "Erreur de saisie" })).StatusCode.ShouldBe(HttpStatusCode.OK);
        var after = await (await world.Admin.GetAsync($"/api/courier-statements/{code}")).JsonAsync();
        after.GetProperty("lineCount").GetInt32().ShouldBe(0);
        after.GetProperty("totalDue").GetInt64().ShouldBe(0);
    }
}
