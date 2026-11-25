using Delivery.Domain.Enums;
using Delivery.Domain.Finance;

namespace Delivery.UnitTests.Domain;

/// <summary>Sens des règlements de l'en-tête du jour et sélection des factures d'un ordre de virement.</summary>
public sealed class TransferRulesTests
{
    private static (long Due, long Cash, long Cheque, long Transfer) Day(params (long Due, (PaymentMethod, long)[] Payments)[] invoices)
    {
        long due = 0, cash = 0, cheque = 0, transfer = 0;
        foreach (var invoice in invoices)
        {
            due += invoice.Due;
            var split = TransferRules.PaymentSplit(invoice.Due, invoice.Payments);
            cash += split.Cash;
            cheque += split.Cheque;
            transfer += split.Transfer;
        }
        return (due, cash, cheque, transfer);
    }

    [Fact]
    public void Cash_follows_the_total_when_a_day_holds_an_invoice_at_minus_65()
    {
        // Les 9 factures du 24/11/2026 chez Speedy, toutes réglées en espèces ; la facture à 0 n'a aucun règlement.
        long[] dues = [152600, 18000, 60000, -6500, 22702835, 116800, 0, 407000, 54900];
        var day = Day(dues.Select(d => (d, d == 0 ? Array.Empty<(PaymentMethod, long)>() : [(PaymentMethod.CASH, Math.Abs(d))])).ToArray());
        day.Due.ShouldBe(23505635);
        day.Cash.ShouldBe(day.Due);
    }

    [Fact]
    public void Every_payment_method_takes_the_sign_of_its_invoice()
    {
        var day = Day((800000, [(PaymentMethod.CASH, 600000), (PaymentMethod.CHECK, 200000)]), (-205000, [(PaymentMethod.TRANSFER, 205000)]), (-6500, [(PaymentMethod.CHECK, 6500)]));
        day.Cash.ShouldBe(600000);
        day.Cheque.ShouldBe(193500);
        day.Transfer.ShouldBe(-205000);
        TransferRules.SignedPayment(-6500, 6500).ShouldBe(-6500);
        TransferRules.SignedPayment(0, 0).ShouldBe(0);
    }

    [Fact]
    public void The_daily_button_offers_net_zero_and_positive_invoices_never_negative_stamped_or_linked_ones()
    {
        TransferRules.IsTransferable(100, false, false).ShouldBeTrue();
        TransferRules.IsTransferable(0, false, false).ShouldBeTrue();
        TransferRules.IsTransferable(-6500, false, false).ShouldBeFalse();
        TransferRules.IsTransferable(100, true, false).ShouldBeFalse();
        TransferRules.IsTransferable(100, false, true).ShouldBeFalse();
    }

    [Fact]
    public void A_transfer_order_takes_issued_invoices_at_net_zero_or_more_and_paid_ones_only_at_net_zero()
    {
        TransferRules.EntersTransferOrder(InvoiceStatus.ISSUED, 100).ShouldBeTrue();
        TransferRules.EntersTransferOrder(InvoiceStatus.ISSUED, 0).ShouldBeTrue();
        TransferRules.EntersTransferOrder(InvoiceStatus.PAID, 0).ShouldBeTrue();
        TransferRules.EntersTransferOrder(InvoiceStatus.PAID, 100).ShouldBeFalse();
        TransferRules.EntersTransferOrder(InvoiceStatus.ISSUED, -6500).ShouldBeFalse();
        TransferRules.EntersTransferOrder(InvoiceStatus.DRAFT, 100).ShouldBeFalse();
        TransferRules.EntersSelection(0).ShouldBeTrue();
        TransferRules.EntersSelection(-6500).ShouldBeFalse();
    }

    [Fact]
    public void Snowdrop_at_net_zero_reads_40_40_0_and_collected_excludes_cheques()
    {
        // SNOWDROP : COD 880, chèque 840, frais 40 → net 0. RANIA : COD 1 000, chèque 300, frais 50 → net 650.
        var lines = TransferRules.LinesFor([
            new TransferInvoice("FA-2611-0123", "snowdrop", "SNOWDROP", 4000, 88000 - 84000 - 4000, 2),
            new TransferInvoice("FA-2611-0124", "rania", "RANIA", 5000, 100000 - 30000 - 5000, 1),
            new TransferInvoice("FA-2611-0125", "rania", "RANIA", 1000, 9000, 1),
        ]);
        var snowdrop = lines.Single(l => l.ClientId == "snowdrop");
        (snowdrop.Collected, snowdrop.Fees, snowdrop.Due).ShouldBe((4000L, 4000L, 0L));
        snowdrop.InvoiceCodes.ShouldBe(["FA-2611-0123"]);
        var rania = lines.Single(l => l.ClientId == "rania");
        (rania.Collected, rania.Fees, rania.Due).ShouldBe((80000L, 6000L, 74000L));
        rania.ParcelCount.ShouldBe(2);
        rania.InvoiceCodes.ShouldBe(["FA-2611-0124", "FA-2611-0125"]);
        lines.Sum(l => l.Collected - l.Fees).ShouldBe(lines.Sum(l => l.Due));
    }
}
