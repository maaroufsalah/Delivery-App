namespace Delivery.Domain.Parcels;

/// <summary>Données de `statuses.ts` et `transitions.ts`, dans leur ordre.</summary>
public static partial class StatusCatalog
{
    public static readonly IReadOnlyList<StatusDefinition> All =
    [
        new(1, "awaiting-pickup", "initial", "#D35400", "Hourglass"),
        new(2, "awaiting-arrival", "initial", "#E67E22", "MapPin"),
        new(3, "picked-up", "pivot", "#3498DB", "Package"),
        new(4, "delivered", "terminal", "#6AB04C", "UserCheck"),
        new(5, "returned", "terminal", "#D63031", "RotateCcw"),
        new(6, "refused", "failure", "#ED4C67", "X"),
        new(7, "shipped", "transit", "#2F3640", "Truck"),
        new(8, "out-of-zone", "failure", "#2D3436", "MapPinOff"),
        new(9, "unreachable", "failure", "#D63031", "PhoneOff"),
        new(10, "wrong-number", "failure", "#576574", "TriangleAlert"),
        new(11, "no-answer-1", "retry", "#341F97", "Phone"),
        new(12, "shipping-in-progress", "transit", "#D63031", "RefreshCw"),
        new(13, "out-for-delivery", "pivot", "#4B4B4B", "Navigation"),
        new(14, "postponed", "retry", "#8C7AE6", "CalendarClock"),
        new(16, "awaiting-delivery", "pivot", "#FFA801", "Inbox"),
        new(17, "pickup-in-progress", "transit", "#4B4B4B", "PackageSearch"),
        new(18, "picked-up-by-courier", "pivot", "#2980B9", "PackageCheck"),
        new(19, "cancelled-by-seller", "failure", "#E15F41", "Ban"),
        new(20, "awaiting-return", "return", "#D63031", "Hourglass"),
        new(21, "recovery-in-progress", "return", "#D35400", "Hourglass"),
        new(22, "recovered-by-courier", "return", "#F97F51", "PackageCheck"),
        new(23, "return-collected", "terminal", "#F97F51", "PackageOpen"),
        new(24, "return-in-progress", "return", "#D35400", "Undo2"),
        new(25, "voicemail", "retry", "#341F97", "Voicemail"),
        new(26, "customer-travelling", "retry", "#341F97", "Plane"),
        new(27, "cancelled-by-customer", "failure", "#341F97", "CircleOff"),
        new(28, "already-delivered", "failure", "#341F97", "PackageCheck"),
        new(29, "not-interested", "failure", "#341F97", "ThumbsDown"),
        new(31, "in-stock", "pivot", "#341F97", "Warehouse"),
        new(32, "no-answer-2", "retry", "#341F97", "Phone"),
        new(33, "no-answer-3", "retry", "#341F97", "Phone"),
        new(34, "no-answer-1-sms", "retry", "#341F97", "MessageSquare"),
        new(35, "no-answer-2-sms", "retry", "#341F97", "MessageSquare"),
        new(37, "wrong-destination", "failure", "#341F97", "MapPinX"),
        new(38, "scheduled", "initial", "#1E40AF", "CalendarCheck"),
    ];

    public static readonly IReadOnlyDictionary<int, int[]> Nominal = new Dictionary<int, int[]>
    {
        [1] = [3, 17, 18, 19, 27, 38],
        [2] = [3, 4, 19, 27, 38],
        [3] = [4, 7, 12, 16, 19, 31],
        [4] = [],
        [5] = [22, 23],
        [6] = [13, 20],
        [7] = [4, 13, 14, 16, 37],
        [8] = [13, 20, 37],
        [9] = [4, 11, 13, 20],
        [10] = [13, 20],
        [11] = [4, 13, 14, 20, 32, 34],
        [12] = [7, 16, 37],
        [13] = [4, 6, 8, 9, 10, 11, 14, 25, 26, 27, 28, 29, 32, 33, 34, 35, 37],
        [14] = [4, 13, 20],
        [16] = [3, 4, 7, 13, 31, 37],
        [17] = [8, 18, 19, 27],
        [18] = [3, 19, 27],
        [19] = [20],
        [20] = [21, 22, 24],
        [21] = [22, 24],
        [22] = [23, 24],
        [23] = [],
        [24] = [22, 23],
        [25] = [4, 11, 13, 14, 20],
        [26] = [4, 13, 14, 20],
        [27] = [20],
        [28] = [20],
        [29] = [20],
        [31] = [13, 16, 20],
        [32] = [4, 13, 14, 20, 33, 35],
        [33] = [4, 13, 14, 20],
        [34] = [4, 13, 14, 20, 32],
        [35] = [4, 13, 14, 20, 33],
        [37] = [13, 16, 20],
        [38] = [1, 2, 3, 19, 27],
    };

    public static readonly int[] LockedStatusIds = [4, 23];
    public static readonly int[] DisputeTargets = [6, 9, 13, 28, 37];
    public static readonly (int From, int To)[] ReturnReception = [(6, 22), (8, 22), (9, 22), (10, 22), (11, 22), (14, 22), (19, 22), (20, 22), (25, 22), (26, 22), (27, 22), (28, 22), (29, 22), (32, 22), (33, 22), (34, 22), (35, 22), (37, 22)];
    public static readonly int[] DestinationLockedStatusIds = [4, 5, 23, 19, 27];
}
