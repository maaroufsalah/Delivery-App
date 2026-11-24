using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Enums;

namespace Delivery.Application.Common.Security;

/// <summary>
/// L'identité « élevée » d'un geste système (`elevated` chez Speedy) : la même personne, vue comme administrateur,
/// sans portée livreur ni vendeur. Sert au miroir des statuts d'un prestataire, qui passe par la correction de statut.
/// </summary>
public sealed class ElevatedCaller(ICurrentUser inner) : ICurrentUser
{
    public bool IsAuthenticated => inner.IsAuthenticated;
    public string UserId => inner.UserId;
    public string? CompanyId => inner.CompanyId;
    public string Email => inner.Email;
    public IReadOnlyList<RoleName> Roles { get; } = inner.Roles.Append(RoleName.SUPER_ADMIN).Distinct().ToList();
    public string? AgencyId => inner.AgencyId;
    public string? ClientId => null;
    public string? CourierId => null;
    public bool MustChangePassword => false;
    public bool Impersonated => inner.Impersonated;
    public long? IssuedAt => inner.IssuedAt;
    public string? IpAddress => inner.IpAddress;
    public string? UserAgent => inner.UserAgent;
    public bool IsMobileClient => inner.IsMobileClient;
    public string RequestId => inner.RequestId;
    public string? PreferredLocale => inner.PreferredLocale;
}
