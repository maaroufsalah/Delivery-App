namespace Delivery.Domain.Enums;

public enum InvoiceAdjustmentKind
{
    ADJUSTMENT,
    PICKUP_NOTE_FEE,
    OTHER,

    /// <summary>Le report d'une facture négative, posé à l'émission (jamais saisi).</summary>
    CARRY_OVER,
}
