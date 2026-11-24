using Delivery.Domain.Enums;

namespace Delivery.Domain.Parcels;

/// <summary>`financial-statuses.ts` : l'état de l'argent d'un colis, interne et vu du vendeur.</summary>
public static class FinancialStatuses
{
    public static readonly string[] Internal = ["non-encaisse", "encaisse", "remis-livreur", "facture", "rembourse"];
    public static readonly string[] Merchant = ["non-encaisse", "encaisse", "facture", "paye"];

    public static string MerchantOf(string status) => status switch
    {
        "rembourse" => "paye",
        "facture" => "facture",
        "encaisse" or "remis-livreur" => "encaisse",
        _ => "non-encaisse",
    };

    public static IEnumerable<string> InternalOf(string merchantStatus) => Internal.Where(s => MerchantOf(s) == merchantStatus);

    public static string Wire(FinancialStatus status) => status.ToString().ToLowerInvariant().Replace('_', '-');

    public static FinancialStatus FromWire(string status) => Enum.Parse<FinancialStatus>(status.ToUpperInvariant().Replace('-', '_'));

    public static int Rank(string status) => Array.IndexOf(Internal, status);

    public static bool Advances(string from, string to) => Rank(to) > Rank(from);

    /// <summary>`financialStatusFrom`.</summary>
    public static string From(string billingState, bool reimbursedByCourier, bool collected) =>
        billingState == "reimbursed" ? "rembourse"
        : billingState == "invoiced" ? "facture"
        : reimbursedByCourier ? "remis-livreur"
        : collected ? "encaisse"
        : "non-encaisse";

    /// <summary>`codToCollect` : ce que le livreur doit encaisser (centimes).</summary>
    public static long CodToCollect(long codAmount, long deliveryFee, bool feeIncluded, bool alreadyPaid)
    {
        if (feeIncluded) return alreadyPaid ? 0 : codAmount;
        if (alreadyPaid) return deliveryFee;
        return codAmount + deliveryFee;
    }
}
