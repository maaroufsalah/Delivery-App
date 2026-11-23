namespace Delivery.Application.Common.Interfaces;

public enum JwtKind
{
    Access,
    Refresh,
}

/// <summary>
/// Signature HS256 des jetons, charge utile identique à Speedy :
/// `sub, cid, email, roles, aid, clid, crid, mcp, skd, imp?, jti?, iat, exp`.
/// </summary>
public interface IJwtService
{
    string Sign(JwtKind kind, IReadOnlyDictionary<string, object?> payload);

    /// <summary>Charge utile d'un jeton valide, sinon <c>null</c>.</summary>
    JwtClaims? Verify(JwtKind kind, string token, bool ignoreExpiration = false);
}

public sealed record JwtClaims(
    string Sub,
    string? Cid,
    string Email,
    IReadOnlyList<string> Roles,
    string? Aid,
    string? Clid,
    string? Crid,
    bool Mcp,
    bool Imp,
    string? Skd,
    string? Jti,
    string? Purpose,
    long? Iat,
    long? Exp);
