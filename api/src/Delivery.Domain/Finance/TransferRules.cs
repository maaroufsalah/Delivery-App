using Delivery.Domain.Enums;

namespace Delivery.Domain.Finance;

/// <summary>Une facture vue par l'ordre de virement (centimes) : son net, ses frais, ses colis.</summary>
public sealed record TransferInvoice(string Code, string ClientId, string ClientName, long Fees, long Due, int ParcelCount);

/// <summary>Une ligne d'OV calculée depuis des factures, avec les factures qu'elle vire réellement.</summary>
public sealed record TransferInvoiceLine(string ClientId, string ClientName, int ParcelCount, long Collected, long Fees, long Due, IReadOnlyList<string> InvoiceCodes);

/// <summary>Règles de l'ordre de virement et du sens des règlements (Speedy 110590e, 018fa2d, e76acc0).</summary>
public static class TransferRules
{
    /// <summary>
    /// Un règlement est saisi en positif ; son sens est celui de la facture : versé au vendeur (net ≥ 0) = +,
    /// encaissé du vendeur (net &lt; 0) = −. Vaut pour les espèces, les chèques et les virements.
    /// </summary>
    public static long SignedPayment(long invoiceDue, long amount) => invoiceDue < 0 ? -amount : amount;

    /// <summary>Les règlements d'une facture répartis par méthode, chacun dans le sens de la facture (en-tête du jour).</summary>
    public static (long Cash, long Cheque, long Transfer) PaymentSplit(long invoiceDue, IEnumerable<(PaymentMethod Method, long Amount)> payments)
    {
        long cash = 0, cheque = 0, transfer = 0;
        foreach (var (method, amount) in payments)
        {
            var signed = SignedPayment(invoiceDue, amount);
            if (method == PaymentMethod.CASH) cash += signed;
            else if (method == PaymentMethod.CHECK) cheque += signed;
            else transfer += signed;
        }
        return (cash, cheque, transfer);
    }

    /// <summary>
    /// « À virer » pour le bouton du jour : émise, net ≥ 0 (net 0 « à verser 0 », décision du 25/11/2026 ; net &lt; 0 jamais),
    /// aucun colis déjà viré (estampille d'un OV « sélection ») et liée à aucun OV.
    /// </summary>
    public static bool IsTransferable(long due, bool parcelStamped, bool linked) => due >= 0 && !parcelStamped && !linked;

    /// <summary>
    /// Une facture entre dans un OV « factures » si elle est émise à net ≥ 0, ou payée à net 0 : une facture à zéro naît
    /// soldée à son émission, PAID est son statut normal.
    /// </summary>
    public static bool EntersTransferOrder(InvoiceStatus status, long due) => (status == InvoiceStatus.ISSUED && due >= 0) || (status == InvoiceStatus.PAID && due == 0);

    /// <summary>Une ligne de la pile « sélection de clients » entre dans l'OV si son net est ≥ 0.</summary>
    public static bool EntersSelection(long due) => due >= 0;

    /// <summary>
    /// Regroupe par client les factures retenues. Encaissé = net + frais = COD − chèques (le net retranche déjà le chèque),
    /// donc encaissé − frais = à verser sur chaque ligne et au total.
    /// </summary>
    public static IReadOnlyList<TransferInvoiceLine> LinesFor(IEnumerable<TransferInvoice> invoices)
    {
        var lines = new List<TransferInvoiceLine>();
        foreach (var invoice in invoices)
        {
            var index = lines.FindIndex(l => l.ClientId == invoice.ClientId);
            var current = index < 0 ? new TransferInvoiceLine(invoice.ClientId, invoice.ClientName, 0, 0, 0, 0, []) : lines[index];
            var next = current with
            {
                ParcelCount = current.ParcelCount + invoice.ParcelCount, Fees = current.Fees + invoice.Fees, Due = current.Due + invoice.Due,
                Collected = current.Collected + invoice.Due + invoice.Fees, InvoiceCodes = [.. current.InvoiceCodes, invoice.Code],
            };
            if (index < 0) lines.Add(next);
            else lines[index] = next;
        }
        return lines;
    }
}
