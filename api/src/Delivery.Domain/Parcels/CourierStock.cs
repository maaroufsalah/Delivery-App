namespace Delivery.Domain.Parcels;

/// <summary>Un circuit du colis, tel que le stock le lit.</summary>
public sealed record CourierStockRouteLink(string CourierId, string Kind, string Status);

/// <summary>Les compteurs d'un livreur (`CourierStockCounters`), en centimes.</summary>
public class CourierStockCounters
{
    public int InHand { get; set; }
    public int PickedUp { get; set; }
    public int OutForDelivery { get; set; }
    public int Awaiting { get; set; }
    public int FailedOrReturning { get; set; }
    public int DeliveredUnremitted { get; set; }
    public long CollectedUnremittedInCents { get; set; }
    public long ToCollectInCents { get; set; }
    public DateTime? LastMovementAt { get; set; }
}

/// <summary>`courier-stock.ts` : ce qu'un livreur a en main, et l'argent qu'il porte.</summary>
public static class CourierStock
{
    public const string PickedUpBlock = "picked-up";
    public const string OutForDeliveryBlock = "out-for-delivery";
    public const string AwaitingBlock = "awaiting";
    public const string FailedOrReturningBlock = "failed-or-returning";
    public const string DeliveredUnremittedBlock = "delivered-unremitted";

    /// <summary>`classifyCourierStock` : le bloc du colis chez ce livreur, ou rien.</summary>
    public static string? Classify(int statusId, string? parcelCourierId, bool reimbursedByCourier, IReadOnlyList<CourierStockRouteLink> routes, IReadOnlyList<int> pastStatusIds,
        string courierId)
    {
        if (parcelCourierId != courierId) return null;
        var mine = routes.Where(l => l.CourierId == courierId).ToList();
        static bool Open(CourierStockRouteLink l) => !string.Equals(l.Status, "CLOSED", StringComparison.OrdinalIgnoreCase);
        static bool Is(CourierStockRouteLink l, string kind) => string.Equals(l.Kind, kind, StringComparison.OrdinalIgnoreCase);
        switch (statusId)
        {
            case 18: return PickedUpBlock;
            case 13: return OutForDeliveryBlock;
            case 7: return mine.Any(l => Is(l, "DELIVERY")) ? OutForDeliveryBlock : null;
            case 16: return mine.Any(Open) ? AwaitingBlock : null;
            case StatusCatalog.Delivered: return reimbursedByCourier ? null : DeliveredUnremittedBlock;
            case 21 or 24: return FailedOrReturningBlock;
            case 22: return mine.Any(l => Is(l, "RETURN") && Open(l)) ? FailedOrReturningBlock : null;
        }
        if (!StatusCatalog.IsCourierReturnStatus(statusId)) return null;
        var delivered = mine.Any(l => Is(l, "DELIVERY"));
        var heldAfterPickup = pastStatusIds.Contains(18) && !pastStatusIds.Contains(3);
        return delivered || heldAfterPickup ? FailedOrReturningBlock : null;
    }

    public static bool IsInHand(string block) => block != DeliveredUnremittedBlock;

    public static bool ExpectsCollection(int statusId, string block) => IsInHand(block) && StatusCatalog.CanTransition(statusId, StatusCatalog.Delivered);

    /// <summary>`addToCourierStock`.</summary>
    public static void Add(CourierStockCounters counters, string block, int statusId, long codInCents, DateTime? lastMovementAt)
    {
        switch (block)
        {
            case PickedUpBlock: counters.PickedUp++; break;
            case OutForDeliveryBlock: counters.OutForDelivery++; break;
            case AwaitingBlock: counters.Awaiting++; break;
            case FailedOrReturningBlock: counters.FailedOrReturning++; break;
            case DeliveredUnremittedBlock:
                counters.DeliveredUnremitted++;
                counters.CollectedUnremittedInCents += codInCents;
                break;
        }
        if (IsInHand(block)) counters.InHand++;
        if (ExpectsCollection(statusId, block)) counters.ToCollectInCents += codInCents;
        counters.LastMovementAt = Latest(counters.LastMovementAt, lastMovementAt);
    }

    /// <summary>`sumCourierStock`.</summary>
    public static void Sum(CourierStockCounters into, CourierStockCounters right)
    {
        into.InHand += right.InHand;
        into.PickedUp += right.PickedUp;
        into.OutForDelivery += right.OutForDelivery;
        into.Awaiting += right.Awaiting;
        into.FailedOrReturning += right.FailedOrReturning;
        into.DeliveredUnremitted += right.DeliveredUnremitted;
        into.CollectedUnremittedInCents += right.CollectedUnremittedInCents;
        into.ToCollectInCents += right.ToCollectInCents;
        into.LastMovementAt = Latest(into.LastMovementAt, right.LastMovementAt);
    }

    private static DateTime? Latest(DateTime? left, DateTime? right) => left is null ? right : right is null ? left : left >= right ? left : right;
}
