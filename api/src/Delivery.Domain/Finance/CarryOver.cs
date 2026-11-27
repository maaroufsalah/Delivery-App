using System.Globalization;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Finance;

/// <summary>
/// Le report des factures négatives (`carry-over.ts`) : une facture client à net négatif n'est jamais réglée en espèces,
/// son montant se reporte sur la prochaine facture émise du vendeur. L'émission et le rattrapage lisent la même règle.
/// </summary>
public static class CarryOver
{
    /// <summary>Le créateur des lignes de report : aucune n'est saisie à la main.</summary>
    public const string AuthorName = "Report automatique";

    /// <summary>
    /// Le report s'applique-t-il à ce vendeur ? Exclus par TYPE, jamais par nom : le Client Pro, le client interne (un compte
    /// de la société) et le régime professionnel, dont la facture ne réclame que des frais (négative par construction).
    /// </summary>
    public static bool Applies(bool isPro, bool isInternal, BillingType billingType) => !isPro && !isInternal && billingType != BillingType.PROFESSIONAL;

    /// <summary>La facture naît-elle « Reportée » ? Net négatif d'un vendeur soumis au report.</summary>
    public static bool CarriesOver(long dueCents, bool isPro, bool isInternal, BillingType billingType) => dueCents < 0 && Applies(isPro, isInternal, billingType);

    /// <summary>
    /// Un règlement est-il refusé ? Une facture « Reportée », ou émise à net négatif d'un vendeur soumis au report. Les
    /// clients pro et internes continuent de régler leurs frais.
    /// </summary>
    public static bool RefusesPayment(InvoiceStatus status, long dueCents, bool isPro, bool isInternal, BillingType billingType) =>
        status == InvoiceStatus.CARRIED_OVER || CarriesOver(dueCents, isPro, isInternal, billingType);

    /// <summary>Le montant de la ligne de report, dû par le vendeur (positif) : l'opposé du net reporté.</summary>
    public static long LineAmount(long carriedDueCents) => -carriedDueCents;

    /// <summary>« Report FA-2611-0120 : −65,00 MAD » ; `amountCents` dû par le vendeur.</summary>
    public static string Reason(string code, long amountCents) =>
        $"Report {code} : −{(amountCents / 100m).ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',')} MAD";
}
