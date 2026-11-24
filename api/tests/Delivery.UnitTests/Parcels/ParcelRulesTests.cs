using Delivery.Application.Audit.Journal;
using Delivery.Application.Common.Documents;
using Delivery.Application.Parcels;
using Delivery.Domain.Finance;
using Delivery.Domain.Parcels;
using Delivery.Domain.Pricing;
using Delivery.Infrastructure.Documents;

namespace Delivery.UnitTests.Parcels;

public sealed class ParcelRulesTests
{
    [Fact]
    public void Parcel_code_reads_origin_day_rank_year_month_destination()
    {
        ParcelCodes.Build("cas", "rab", 7, new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc)).ShouldBe("CAS030072611RAB");
        ParcelCodes.Build("CAS", "RAB", 1234, new DateTime(2026, 1, 30, 0, 0, 0, DateTimeKind.Utc)).ShouldBe("CAS3012342601RAB");
    }

    [Theory]
    [InlineData("06 12 34 56 78", new[] { "0612345678" })]
    [InlineData("0612345678 0712345678", new[] { "0612345678", "0712345678" })]
    [InlineData("06-12-34-56-78 / 06.12.34.56.78", new[] { "0612345678" })]
    [InlineData("00212612345678", new[] { "+212612345678" })]
    public void Recipient_phones_are_split_normalized_and_deduplicated(string input, string[] expected) =>
        ParcelInputs.SplitPhones(input).ShouldBe(expected);

    [Fact]
    public void Failure_fee_follows_the_parcel_into_the_return_flow()
    {
        FailureFees.For(5, sameCity: false, PricingPolicy.Default).ShouldBe(1000);
        FailureFees.For(3, sameCity: false, PricingPolicy.Default).ShouldBeNull();
        FailureFees.AfterTransition(true, null, 20, false, 1000).ShouldBe((true, 1000, false));
        FailureFees.AfterTransition(true, 500, 22, false, 1000).ShouldBe((true, null, true));
        FailureFees.AfterTransition(false, null, 9, true, 1000).ShouldBe((true, 1000, false));
    }

    [Fact]
    public void Net_due_takes_the_fee_and_its_vat_off_the_cod()
    {
        var delivered = new BillableParcel(19990, 2000, null, 0, true, false, 4);
        Billing.NetDueFor("normal", delivered).ShouldBe(17990);
        Billing.NetDueFor("normal", delivered, vatRate: 2000).ShouldBe(17590);
        Billing.NetDueFor("professional", delivered).ShouldBe(-2000);
        Billing.FeeCharged(delivered with { StatusId = 5, FailureFee = 1000 }).ShouldBe(1000);
        Billing.VatOnTotal(-1000, 2000).ShouldBe(-200);
    }

    [Fact]
    public void Provider_status_map_defaults_to_identity_and_ignores_an_invalid_override()
    {
        ProviderStatusMap.Effective(null)[4].ShouldBe(4);
        var map = ProviderStatusMap.Effective("{\"9\":null,\"30\":13}");
        map[9].ShouldBeNull();
        map[30].ShouldBe(13);
        ProviderStatusMap.Effective("{\"1\":4}")[1 + 2].ShouldBe(3);
    }

    [Fact]
    public void Journal_value_texts_read_like_the_screen()
    {
        JournalPresenter.ValueText(null).ShouldBe("—");
        JournalPresenter.ValueText(new Dictionary<string, object> { ["kind"] = "bool", ["value"] = true }).ShouldBe("Oui");
        JournalPresenter.RoleOf("SUPER_ADMIN").ShouldBe("admin");
        PdfText.Amount(19990).ShouldBe("199,90 MAD");
        PdfText.Truncate("abcdef", 4).ShouldBe("abc…");
    }

    [Fact]
    public void Label_sheet_prints_one_label_per_carton()
    {
        var sheet = new LabelSheet(LabelSheet.Classic, LabelSheet.Format10X10, "Wimo", null, [new LabelData { Code = "A", Quantity = 3 }, new LabelData { Code = "B" }]);
        var labels = sheet.Expanded();
        labels.Count.ShouldBe(4);
        PdfText.CartonMention(labels[1]).ShouldBe("2/3");
        PdfText.CartonMention(labels[3]).ShouldBeNull();
        new PdfService().Render(sheet).Take(5).ShouldBe("%PDF-"u8.ToArray());
    }
}
