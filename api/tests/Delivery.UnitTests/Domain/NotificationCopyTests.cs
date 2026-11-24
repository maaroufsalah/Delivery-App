using Delivery.Application.Notifications;

namespace Delivery.UnitTests.Domain;

/// <summary>`notification-copy.ts` / `plural.ts` : le texte d'un push, dans la langue de l'appareil.</summary>
public sealed class NotificationCopyTests
{
    [Theory]
    [InlineData("fr", "1", "1 colis vous est affecté.")]
    [InlineData("fr", "0", "0 colis vous est affecté.")]
    [InlineData("fr", "4", "4 colis vous sont affectés.")]
    [InlineData("en", "1", "1 parcel has been assigned to you.")]
    [InlineData("en", "3", "3 parcels have been assigned to you.")]
    [InlineData("ar", "2", "أُسنِد إليك طردان.")]
    [InlineData("ar", "5", "أُسنِدت إليك 5 طرود.")]
    [InlineData("ar", "11", "أُسنِد إليك 11 طردًا.")]
    public void Route_assigned_uses_the_plural_of_the_device_language(string locale, string count, string body)
    {
        var copy = NotificationCopy.For("route-assigned", locale, new Dictionary<string, string> { ["count"] = count });
        copy.ShouldNotBeNull();
        copy.Body.ShouldBe(body);
    }

    [Fact]
    public void Unknown_language_falls_back_to_french_and_unknown_topic_to_nothing()
    {
        var copy = NotificationCopy.For("route-withdrawn", "de", new Dictionary<string, string> { ["code"] = "CIR-7" });
        copy!.Title.ShouldBe("Circuit retiré");
        copy.Body.ShouldBe("Le circuit CIR-7 ne vous est plus affecté.");
        NotificationCopy.For("inconnu", "fr", null).ShouldBeNull();
        NotificationCopy.Format("Bonjour {name}", new Dictionary<string, string>(), "fr").ShouldBe("Bonjour {name}");
        NotificationCopy.Topics.Count.ShouldBe(26);
    }

    [Fact]
    public void Catalogue_covers_every_topic_the_notifier_knows()
    {
        Notifier.Topics.Select(t => t.Key).OrderBy(k => k).ShouldBe(NotificationCopy.Topics.OrderBy(k => k));
    }
}
