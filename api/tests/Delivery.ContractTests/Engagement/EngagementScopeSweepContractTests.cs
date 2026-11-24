using System.Net;
using System.Net.Http.Json;
using Delivery.ContractTests.Infrastructure;
using Delivery.ContractTests.Routes;

namespace Delivery.ContractTests.Engagement;

/// <summary>Balayage de portée du module « Engagement » : réclamations, actualités et notifications restent chez leur destinataire.</summary>
[Collection(ApiCollection.Name)]
public sealed class EngagementScopeSweepContractTests(ApiFactory factory)
{
    [Fact]
    public async Task Claims_announcements_and_notifications_stay_within_their_owner()
    {
        var a = await DeliveryNotesContractTests.WorldAsync(factory);
        var b = await DeliveryNotesContractTests.WorldAsync(factory);
        var (_, outsider) = await RoutesContractTests.CourierAsync(factory, b.City.Id);
        var agentB = await TestData.ClientForAsync(factory, await TestData.UserAsync(factory, Delivery.Domain.Enums.RoleName.STAFF, b.Agency.Id));
        var typeId = await ClaimsContractTests.TypeAsync(a);
        var parcel = await DeliveryNotesContractTests.ParcelAsync(a);
        var claim = await ClaimsContractTests.OpenAsync(a, typeId, parcel.Code);
        var code = claim.GetProperty("code").GetString()!;
        var attachment = claim.GetProperty("attachments")[0].GetProperty("id").GetString()!;

        // Vendeur B, livreur étranger et agent d'une autre agence : ni la réclamation, ni sa pièce jointe, ni son fil, ni l'assignation.
        foreach (var reader in new[] { b.Seller, outsider, agentB })
        {
            (await reader.GetAsync($"/api/claims/{code}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await reader.GetAsync($"/api/claims/{code}/attachments/{attachment}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await reader.PostAsJsonAsync($"/api/claims/{code}/events", new { body = "Intrusion" })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await (await reader.GetAsync("/api/claims")).JsonAsync()).GetProperty("total").GetInt32().ShouldBe(0);
            (await (await reader.GetAsync("/api/claims/counters")).JsonAsync()).GetProperty("open").GetInt32().ShouldBe(0);
        }
        (await (await b.Seller.GetAsync($"/api/claims?clientId={a.Merchant.Id}")).JsonAsync()).GetProperty("total").GetInt32().ShouldBe(0);

        // Une actualité réservée aux vendeurs n'atteint pas un livreur ; un brouillon n'atteint personne.
        var merchantsOnly = await (await a.Admin.PostAsJsonAsync("/api/announcements", AnnouncementsContractTests.Body(TestData.Unique("Vendeurs "), "merchants", true))).JsonAsync();
        var draft = await (await a.Admin.PostAsJsonAsync("/api/announcements", AnnouncementsContractTests.Body(TestData.Unique("Brouillon "), "all", false))).JsonAsync();
        (await outsider.GetAsync($"/api/announcements/{merchantsOnly.GetProperty("id").GetString()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await b.Seller.GetAsync($"/api/announcements/{draft.GetProperty("id").GetString()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Les notifications de A : invisibles et intouchables pour B.
        var id = await NotificationsContractTests.NotifyAsync(factory, a.Merchant.UserId!, "parcel-delivered");
        var feed = await (await b.Seller.GetAsync("/api/me/notification-feed")).JsonAsync();
        feed.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString()).ShouldNotContain(id);
        await b.Seller.PostAsync($"/api/me/notification-feed/{id}/read", null);
        await b.Seller.PostAsync("/api/me/notification-feed/read-all", null);
        (await (await a.Seller.GetAsync("/api/me/notification-feed")).JsonAsync()).GetProperty("unread").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
    }
}
