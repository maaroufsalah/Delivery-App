using System.Text.Json.Nodes;
using Delivery.Domain.Common;
using Delivery.Domain.Parcels;
using Delivery.Domain.Pricing;

namespace Delivery.UnitTests.Domain;

public sealed class DeliveryPricingTests
{
    private static PricingContext Context(IReadOnlyList<PriceRule>? rules = null, IReadOnlyList<PriceDiscount>? discounts = null) =>
        new(rules ?? [], discounts ?? [], city => city == "casa" ? "z1" : "z2", zone => zone == "z1" ? (3000, 1500) : (4000, 2000));

    [Fact]
    public void Falls_back_on_the_destination_zone()
    {
        DeliveryPricing.QuoteDeliveryFee(new QuoteInput("casa", "rabat", "district"), Context()).Fee.ShouldBe(4000);
        DeliveryPricing.QuoteDeliveryFee(new QuoteInput("casa", "casa", "district"), Context()).Fee.ShouldBe(1500);
    }

    [Fact]
    public void A_rule_wins_and_depends_on_the_delivery_mode()
    {
        var rules = new[] { new PriceRule("casa", "rabat", 2500, 2200, 2100) };
        DeliveryPricing.QuoteDeliveryFee(new QuoteInput("casa", "rabat", "agency"), Context(rules)).ShouldBe(new Quote(2200, 2200, true, false, false));
        DeliveryPricing.QuoteDeliveryFee(new QuoteInput("casa", "rabat", "relay-point"), Context(rules)).Fee.ShouldBe(2100);
    }

    [Fact]
    public void City_discount_beats_zone_discount_and_multiplier_applies()
    {
        var discounts = new[]
        {
            new PriceDiscount("c1", ["z2"], [], 1800, null, true),
            new PriceDiscount("c1", [], ["rabat"], 1200, null, true),
        };
        var quote = DeliveryPricing.QuoteDeliveryFee(new QuoteInput("casa", "rabat", "district", "c1", 1.5), Context(null, discounts));
        quote.Fee.ShouldBe(1800);
        quote.Discounted.ShouldBeTrue();
    }
}

public sealed class ReferentialHelpersTests
{
    [Theory]
    [InlineData("Fès-El Bali", "feselbali")]
    [InlineData("  Béni Mellal ", "benimellal")]
    public void City_key_drops_accents_and_separators(string input, string expected) => CityNames.CityKey(input).ShouldBe(expected);

    [Fact]
    public void Normalizes_imported_city_names()
    {
        CityNames.Normalize("KENITRA VILLE").Name.ShouldBe("Kénitra");
        var (name, arabic) = CityNames.Normalize("TETOUAN تطوان");
        name.ShouldBe("Tétouan");
        arabic.ShouldBe("تطوان");
    }

    [Fact]
    public void Theme_is_normalized_and_pruned()
    {
        var issues = new List<(string, string)>();
        var parsed = ThemeOverrides.Parse(JsonNode.Parse("""{"light":{"badge":{"background":"#abc","foreground":""},"bogus":{}},"extra":1}"""), "theme", issues);
        issues.ShouldBeEmpty();
        ThemeOverrides.Prune(parsed).ToJsonString().ShouldBe("""{"light":{"badge":{"background":"#AABBCC"}}}""");
    }

    [Fact]
    public void Contrast_follows_wcag() => Colors.ContrastRatio("#FFFFFF", "#000000").ShouldBe(21, 0.001);

    [Fact]
    public void Money_matches_js_rounding()
    {
        Money.ToCents(12.345m).ShouldBe(1235);
        Money.FromMad(35.5).ShouldBe(35.50m);
        Money.ToWire("TO_INVOICE").ShouldBe("to-invoice");
    }

    [Fact]
    public void Status_catalogue_has_dispute_exits_for_locked_statuses()
    {
        StatusCatalog.All.Count.ShouldBe(35);
        StatusCatalog.TransitionRows().ShouldContain((4, 6, "DISPUTE"));
        StatusCatalog.Find(4)!.Key.ShouldBe("delivered");
    }
}
