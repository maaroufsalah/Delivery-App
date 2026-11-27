namespace Delivery.Domain.Enums;

public enum InvoiceStatus
{
    DRAFT,
    ISSUED,
    PAID,

    /// <summary>Net négatif d'un vendeur ordinaire, reporté sur sa facture suivante.</summary>
    CARRIED_OVER,
}
