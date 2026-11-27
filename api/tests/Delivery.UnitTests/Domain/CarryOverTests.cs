using Delivery.Domain.Enums;
using Delivery.Domain.Finance;

namespace Delivery.UnitTests.Domain;

/// <summary>Le report des factures négatives : qui, le libellé, les totaux (`carry-over.spec.ts`).</summary>
public sealed class CarryOverTests
{
    [Fact]
    public void Excluded_by_type_pro_internal_and_professional_regime()
    {
        CarryOver.Applies(false, false, BillingType.NORMAL).ShouldBeTrue();
        CarryOver.Applies(true, false, BillingType.NORMAL).ShouldBeFalse();
        CarryOver.Applies(false, true, BillingType.NORMAL).ShouldBeFalse();
        CarryOver.Applies(false, false, BillingType.PROFESSIONAL).ShouldBeFalse();
    }

    [Fact]
    public void Label_reads_report_FA_2611_0120_minus_65_MAD() => CarryOver.Reason("FA-2611-0120", 6500).ShouldBe("Report FA-2611-0120 : −65,00 MAD");

    [Fact]
    public void Minus_65_then_plus_599_gives_534()
    {
        var lines = new InvoiceLineSums(59900, 0, 0, 0, 59900);
        var carry = CarryOver.LineAmount(-6500);
        carry.ShouldBe(6500);
        var totals = InvoiceMath.Totals(lines, [(carry, 0L)]);
        totals.Due.ShouldBe(53400);
        totals.Adjustment.ShouldBe(6500);
        CarryOver.CarriesOver(totals.Due, false, false, BillingType.NORMAL).ShouldBeFalse();
    }

    [Fact]
    public void Carry_overs_add_up_and_a_still_negative_next_invoice_carries_over_again()
    {
        var totals = InvoiceMath.Totals(new InvoiceLineSums(59900, 0, 0, 0, 59900), [(4000L, 0L), (6500L, 0L)]);
        totals.Due.ShouldBe(49400);
        var small = InvoiceMath.Totals(new InvoiceLineSums(1000, 0, 0, 0, 1000), [(6500L, 0L)]);
        small.Due.ShouldBe(-5500);
        CarryOver.CarriesOver(small.Due, false, false, BillingType.NORMAL).ShouldBeTrue();
    }

    [Fact]
    public void Pro_and_internal_are_never_carried_over()
    {
        CarryOver.CarriesOver(-6500, true, false, BillingType.NORMAL).ShouldBeFalse();
        CarryOver.CarriesOver(-6500, false, true, BillingType.NORMAL).ShouldBeFalse();
        CarryOver.CarriesOver(-6500, false, false, BillingType.PROFESSIONAL).ShouldBeFalse();
    }

    [Fact]
    public void Payment_is_refused_on_a_carried_over_or_negative_invoice_of_an_ordinary_seller()
    {
        CarryOver.RefusesPayment(InvoiceStatus.CARRIED_OVER, -6500, false, false, BillingType.NORMAL).ShouldBeTrue();
        CarryOver.RefusesPayment(InvoiceStatus.ISSUED, -6500, false, false, BillingType.NORMAL).ShouldBeTrue();
        CarryOver.RefusesPayment(InvoiceStatus.ISSUED, 6500, false, false, BillingType.NORMAL).ShouldBeFalse();
        CarryOver.RefusesPayment(InvoiceStatus.ISSUED, -6500, true, false, BillingType.NORMAL).ShouldBeFalse();
        CarryOver.RefusesPayment(InvoiceStatus.ISSUED, -6500, false, true, BillingType.NORMAL).ShouldBeFalse();
    }

    [Fact]
    public void A_carry_over_stays_out_of_the_taxed_base_of_a_pro_invoice()
    {
        var lines = new InvoiceLineSums(0, 10000, 0, 0, -10000);
        var taxed = InvoiceMath.ProTotals(lines, [(6500L, false)], 2000);
        var exempt = InvoiceMath.ProTotals(lines, [(6500L, true)], 2000);
        taxed.Vat.ShouldBe(3300);
        exempt.Vat.ShouldBe(2000);
        exempt.Due.ShouldBe(-10000 - 6500 - 2000);
    }
}
