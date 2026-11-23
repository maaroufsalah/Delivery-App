namespace Delivery.Domain.Common;

/// <summary>Codes d'erreur du contrat Speedy (`ApiErrorCode`).</summary>
public enum ErrorCode
{
    UNAUTHENTICATED,
    FORBIDDEN,
    NOT_FOUND,
    CONFLICT,
    VALIDATION_FAILED,
    TRANSITION_FORBIDDEN,
    PAYLOAD_TOO_LARGE,
    TOO_MANY_REQUESTS,
    INTERNAL,
}

/// <summary>
/// Erreur métier, rendue par l'API sous la forme `{ code, message, details? }`
/// du filtre d'exception global de Speedy.
/// </summary>
public sealed class BusinessException(ErrorCode code, string message, object? details = null)
    : Exception(message)
{
    public ErrorCode Code { get; } = code;

    public object? Details { get; } = details;

    public static BusinessException NotFound(string message, object? details = null) => new(ErrorCode.NOT_FOUND, message, details);

    public static BusinessException Forbidden(string message, object? details = null) => new(ErrorCode.FORBIDDEN, message, details);

    public static BusinessException Unauthenticated(string message = "Authentification requise.", object? details = null) =>
        new(ErrorCode.UNAUTHENTICATED, message, details);

    public static BusinessException Conflict(string message, object? details = null) => new(ErrorCode.CONFLICT, message, details);

    public static BusinessException Validation(string message, object? details = null) => new(ErrorCode.VALIDATION_FAILED, message, details);

    public static BusinessException Transition(string message, object? details = null) => new(ErrorCode.TRANSITION_FORBIDDEN, message, details);

    public static BusinessException TooManyRequests(string message, object? details = null) => new(ErrorCode.TOO_MANY_REQUESTS, message, details);

    public static BusinessException Internal(string message, object? details = null) => new(ErrorCode.INTERNAL, message, details);
}
