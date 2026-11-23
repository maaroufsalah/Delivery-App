namespace Delivery.Domain.Enums;

public enum ParcelStatusGroup
{
    INITIAL,
    TRANSIT,
    PIVOT,
    RETRY,
    FAILURE,
    RETURN,
    TERMINAL,
}
