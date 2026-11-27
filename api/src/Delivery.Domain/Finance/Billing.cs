namespace Delivery.Domain.Finance;

/// <summary>`BillableParcel` (centimes).</summary>
public sealed record BillableParcel(long CodAmount, long DeliveryFee, long? FailureFee, long OtherFees, bool FeeIncluded, bool AlreadyPaid, int StatusId);

/// <summary>`utils/money.ts` : ce qu'un colis rapporte, ce qu'il coûte, ce qui revient au vendeur.</summary>
public static class Billing
{
    public const int DeliveredStatusId = 4;
    public static readonly int[] ProVatRates = [2000, 1400, 1000, 700, 0];
    public const int DefaultProVatRate = 2000;

    /// <summary>`codToCollect`.</summary>
    public static long CodToCollect(long codAmount, long deliveryFee, bool feeIncluded, bool alreadyPaid)
    {
        if (feeIncluded) return alreadyPaid ? 0 : codAmount;
        if (alreadyPaid) return deliveryFee;
        return codAmount + deliveryFee;
    }

    /// <summary>`codPayoutAmount`.</summary>
    public static long CodPayoutAmount(long collected, long chequePaid) => Math.Max(0, collected - chequePaid);

    /// <summary>`codCollected`.</summary>
    public static long CodCollected(BillableParcel parcel, int deliveredStatusId = DeliveredStatusId) =>
        parcel.StatusId == deliveredStatusId ? CodToCollect(parcel.CodAmount, parcel.DeliveryFee, parcel.FeeIncluded, parcel.AlreadyPaid) : 0;

    /// <summary>`feeCharged`.</summary>
    public static long FeeCharged(BillableParcel parcel, int deliveredStatusId = DeliveredStatusId) =>
        parcel.StatusId == deliveredStatusId ? parcel.DeliveryFee + parcel.OtherFees : (parcel.FailureFee ?? 0) + parcel.OtherFees;

    /// <summary>`vatOn` : taux en points de base (2000 = 20 %), arrondi de JS.</summary>
    public static long VatOn(long amountHt, int vatRate) => vatRate <= 0 ? 0 : (long)Math.Floor(amountHt * (double)vatRate / 10_000 + 0.5);

    /// <summary>`proVatRateFor`.</summary>
    public static int? ProVatRateFor(bool isPro, int? clientRate, int companyDefault) => !isPro ? null : clientRate ?? companyDefault;

    /// <summary>`vatOnTotal`.</summary>
    public static long VatOnTotal(long amountHt, int vatRate)
    {
        var vat = VatOn(Math.Abs(amountHt), vatRate);
        return amountHt < 0 && vat > 0 ? -vat : vat;
    }

    /// <summary>`merchantNetDue`.</summary>
    public static long MerchantNetDue(BillableParcel parcel, int deliveredStatusId = DeliveredStatusId, int vatRate = 0)
    {
        var fee = FeeCharged(parcel, deliveredStatusId);
        return CodCollected(parcel, deliveredStatusId) - fee - VatOn(fee, vatRate);
    }

    /// <summary>`netDueFor` : le client professionnel ne touche pas le COD par la facture.</summary>
    public static long NetDueFor(string billingType, BillableParcel parcel, int deliveredStatusId = DeliveredStatusId, int vatRate = 0, long chequePaid = 0)
    {
        var fee = FeeCharged(parcel, deliveredStatusId);
        if (billingType == "professional") return 0 - fee - VatOn(fee, vatRate);
        return CodCollected(parcel, deliveredStatusId) - chequePaid - fee - VatOn(fee, vatRate);
    }
}

/// <summary>Sommes des lignes colis d'une facture (centimes).</summary>
public sealed record InvoiceLineSums(long Cod, long DeliveryFee, long FailureFee, long Vat, long Due);

/// <summary>Totaux d'une facture.</summary>
public sealed record InvoiceTotals(long Cod, long DeliveryFee, long FailureFee, long Adjustment, long Vat, long Due);

/// <summary>`invoiceTotals`, `proInvoiceTotals`, `signedVat` d'`invoices.service.ts`.</summary>
public static class InvoiceMath
{
    /// <summary>`signedVat` : la TVA garde le signe du montant.</summary>
    public static long SignedVat(long amount, int vatRate) => Math.Sign(amount) * Billing.VatOn(Math.Abs(amount), vatRate);

    /// <summary>`invoiceTotals` : les ajustements sont des frais, leur TTC se retranche du net.</summary>
    public static InvoiceTotals Totals(InvoiceLineSums lines, IEnumerable<(long Amount, long VatAmount)> adjustments)
    {
        var list = adjustments.ToList();
        var adjustment = list.Sum(a => a.Amount);
        var adjustmentVat = list.Sum(a => a.VatAmount);
        return new InvoiceTotals(lines.Cod, lines.DeliveryFee, lines.FailureFee, adjustment, lines.Vat + adjustmentVat, lines.Due - adjustment - adjustmentVat);
    }

    /// <summary>`proInvoiceTotals` : la TVA du client Pro se calcule sur le total HT des frais.</summary>
    public static InvoiceTotals ProTotals(InvoiceLineSums lines, IEnumerable<long> adjustments, int vatRate) => ProTotals(lines, adjustments.Select(a => (a, false)), vatRate);

    /// <summary>`proInvoiceTotals` : `VatExempt` (un report `CARRY_OVER`, déjà TTC) n'entre pas dans la base HT taxée.</summary>
    public static InvoiceTotals ProTotals(InvoiceLineSums lines, IEnumerable<(long Amount, bool VatExempt)> adjustments, int vatRate)
    {
        var list = adjustments.ToList();
        var adjustment = list.Sum(a => a.Amount);
        var taxable = list.Sum(a => a.VatExempt ? 0 : a.Amount);
        var vat = Billing.VatOnTotal(lines.DeliveryFee + lines.FailureFee + taxable, vatRate);
        return new InvoiceTotals(lines.Cod, lines.DeliveryFee, lines.FailureFee, adjustment, vat, lines.Due + lines.Vat - adjustment - vat);
    }
}
