using Delivery.Domain.Enums;

namespace Delivery.Application.Common.Interfaces;

/// <summary>
/// L'appelant de la requête (le `AuthenticatedUser` / `RequestScope` de Speedy),
/// plus ce que la requête dit de lui (adresse, agent, client mobile).
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    string UserId { get; }

    /// <summary>Société du jeton (`cid`) : l'unique société de Wimo.</summary>
    string? CompanyId { get; }

    string Email { get; }

    IReadOnlyList<RoleName> Roles { get; }

    string? AgencyId { get; }

    string? ClientId { get; }

    string? CourierId { get; }

    bool MustChangePassword { get; }

    bool Impersonated { get; }

    long? IssuedAt { get; }

    string? IpAddress { get; }

    string? UserAgent { get; }

    /// <summary>En-tête `x-client: mobile`.</summary>
    bool IsMobileClient { get; }

    string RequestId { get; }

    /// <summary>Langue des documents : cookie `NEXT_LOCALE`, sinon `Accept-Language`.</summary>
    string? PreferredLocale { get; }
}
