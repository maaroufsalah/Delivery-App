using Delivery.Domain.Pricing;

namespace Delivery.Domain.Finance;

/// <summary>Une ligne de la grille d'un livreur (`CourierGridRate`), en centimes.</summary>
public sealed record CourierGridRate(string CityId, long PickupFee, long DeliveryFee);

public enum CourseOutcome
{
    Delivery,
    Pickup,
    Failure,
}

/// <summary>La commission d'une course (`courier-pay.ts`).</summary>
public static class CourierPay
{
    public const int Delivered = 4;

    /// <summary>`courseOutcomeOf` : un ramassage paie toujours, une livraison ou un échec selon le statut final.</summary>
    public static CourseOutcome? OutcomeOf(string routeKind, int? finalStatusId)
    {
        if (routeKind == "PICKUP") return CourseOutcome.Pickup;
        if (finalStatusId is null) return null;
        if (finalStatusId == Delivered) return CourseOutcome.Delivery;
        return FailureFees.IsFailureFeeStatus(finalStatusId.Value) ? CourseOutcome.Failure : null;
    }

    /// <summary>
    /// `courierCommissionFor` : négocié, sinon réglage d'échec, sinon la grille du livreur pour la ville DU COLIS (livraison :
    /// ville de livraison ; ramassage : ville de ramassage). Comme Wimo Laravel (`CircuitController` : `villes_where($colis->…_ville_id)`,
    /// `LivreurFactureService` : « une ville absente de la grille tombe directement dans le cas frais = 0 »), aucune autre
    /// ville ne s'y substitue : la ville du circuit n'est plus un repli.
    /// </summary>
    public static (long Amount, string Source) CommissionFor(CourseOutcome outcome, string cityId, long? negotiatedFee, IReadOnlyList<CourierGridRate> grid, long failedCommission)
    {
        if (negotiatedFee is { } negotiated) return (negotiated, "NEGOTIATED");
        if (outcome == CourseOutcome.Failure) return (failedCommission, "FAILURE_SETTING");
        var row = grid.FirstOrDefault(r => r.CityId == cityId);
        return (row is null ? 0 : outcome == CourseOutcome.Pickup ? row.PickupFee : row.DeliveryFee, "GRID");
    }

    /// <summary>`chequePaidOn` : un chèque non annulé compte entier ; annulé, seulement ce que la recette en a retenu.</summary>
    public static long ChequePaidOn(long? chequeAmount, bool chequeCancelled, long? remittedCheque)
    {
        if (chequeAmount is not { } amount) return 0;
        if (!chequeCancelled) return amount;
        return remittedCheque is { } remitted ? Math.Min(amount, Math.Max(0, remitted)) : amount;
    }
}
