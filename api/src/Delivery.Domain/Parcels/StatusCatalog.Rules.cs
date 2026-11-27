namespace Delivery.Domain.Parcels;

/// <summary>Règles de `statuses.ts` et `transitions.ts` qui ne sont pas de simples tables.</summary>
public static partial class StatusCatalog
{
    public const int AwaitingPickup = 1;
    public const int AwaitingArrival = 2;
    public const int PickedUp = 3;
    public const int Delivered = 4;
    public const int Returned = 5;
    public const int Refused = 6;
    public const int OutForDelivery = 13;
    public const int AwaitingDelivery = 16;
    public const int ReturnPending = 20;
    public const int ReturnedToAgency = 22;
    public const int ReturnedToClient = 23;

    public static readonly int[] BillableStatusIds = [Delivered, Returned, Refused];
    public static readonly int[] ReturnFlowStatusIds = [ReturnPending, ReturnedToAgency, ReturnedToClient];
    private static readonly int[] FailureCarryIds = [20, 21, 22, 23, 24];

    public static string? GroupOf(int statusId) => Find(statusId)?.Group;

    public static bool IsEditableBySeller(int statusId) => GroupOf(statusId) == "initial";

    public static bool IsTerminal(int statusId) => GroupOf(statusId) == "terminal";

    public static bool IsFailure(int statusId) => GroupOf(statusId) is "failure" or "retry";

    public static bool IsBillable(int statusId) => BillableStatusIds.Contains(statusId);

    public static bool IsBillableParcel(int statusId, bool isFailed) => IsBillable(statusId) || (isFailed && FailureCarryIds.Contains(statusId));

    public static bool IsReturnEligible(int statusId) => !ReturnExcludedStatusIds.Contains(statusId);

    public static bool IsCourierReturnStatus(int statusId) => CourierReturnStatusIds.Contains(statusId);

    public static bool IsReturnFlow(int statusId) => ReturnFlowStatusIds.Contains(statusId);

    public static bool IsDestinationLocked(int statusId) => DestinationLockedStatusIds.Contains(statusId);

    /// <summary>`merchantReturnStage`.</summary>
    public static string MerchantReturnStage(int statusId) => MerchantReturnedStatusIds.Contains(statusId) ? "returned" : "on-the-way";

    /// <summary>`courierOutcomesFor` : les issues qu'un livreur peut déclarer, selon le genre du circuit.</summary>
    public static int[] CourierOutcomesFor(int statusId, string? routeKind = null) =>
        CourierOutcomes.TryGetValue((statusId, routeKind?.ToUpperInvariant() ?? string.Empty), out var outcomes) ? outcomes : [];

    public static bool CanCourierReport(int from, int to, string? routeKind = null) => CourierOutcomesFor(from, routeKind).Contains(to);

    public static bool IsCourierDeliveryPending(int statusId) =>
        statusId != Delivered && (statusId == OutForDelivery || CourierDeliveryOutcomeIds.Contains(statusId));

    public static int[] AdminFieldStatusIds() => CourierDeliveryOutcomeIds.Where(id => id != Delivered).ToArray();

    public static int[] AdminFieldTargets(int statusId) => AdminFieldTargetsById.TryGetValue(statusId, out var targets) ? targets : [];

    public static bool CanAdminReviseField(int from, int to) => AdminFieldTargets(from).Contains(to);

    public static IReadOnlyList<int> DisputeTransitions(int statusId) => IsLocked(statusId) ? DisputeTargets.Where(t => t != statusId).ToArray() : [];

    private static string Key(string kind) => kind.ToLowerInvariant();

    /// <summary>
    /// Une nouvelle tentative de livraison : « Injoignable » (9), « Pas de réponse » (11, 32 à 35), « Boîte vocale » (25),
    /// « Client en voyage » (26), « Reporté » (14). Comme dans Wimo Laravel (`CircuitController` : un circuit de livraison
    /// reprend tout colis non livré, non retourné et non annulé), ils repartent dans le circuit suivant.
    /// </summary>
    public static readonly int[] RedeliveryStatusIds = [9, 11, 14, 25, 26, 32, 33, 34, 35];

    public static int[] RouteEligibleStatusIds(string kind) => Key(kind) switch
    {
        "delivery" => [3, .. RedeliveryStatusIds],
        "pickup" => [1, 17],
        "return" => [20, 22],
        _ => [],
    };

    public static bool IsRouteEligible(string kind, int statusId) => RouteEligibleStatusIds(kind).Contains(statusId);

    public static int RouteDispatchStatusId(string kind) => Key(kind) switch
    {
        "pickup" => 17,
        "return" => 24,
        _ => 13,
    };

    /// <summary>`routeDispatchPath` : le chemin du statut courant au statut « en route », sinon <c>null</c>.</summary>
    public static int[]? RouteDispatchPath(string kind, int from)
    {
        var target = RouteDispatchStatusId(kind);
        if (from == target) return [];
        if (CanTransition(from, target)) return [target];
        if (Key(kind) == "delivery" && CanTransition(from, AwaitingDelivery) && CanTransition(AwaitingDelivery, target)) return [AwaitingDelivery, target];
        return null;
    }

    public static int RouteExpectedOutcome(string kind) => Key(kind) switch
    {
        "pickup" => 18,
        "return" => 23,
        _ => 4,
    };

    public static bool RouteParcelAwaitsOutcome(string kind, int statusId, bool confirmed) =>
        Key(kind) == "pickup" ? statusId != 18 && statusId != 3 : !confirmed;

    public static bool RouteTargetsSender(string kind) => Key(kind) is "pickup" or "return";

    public static string? RouteKindForParcelStatus(int statusId) =>
        new[] { "delivery", "pickup", "return" }.FirstOrDefault(kind => IsRouteEligible(kind, statusId));
}
