namespace Delivery.Domain.Enums;

public enum ProviderCallOutcome
{
    APPLIED,
    WOULD_APPLY,
    ALREADY_APPLIED,
    IGNORED,
    STALE,
    REJECTED,
    WOULD_REJECT,
}
