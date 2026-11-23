using System.Security.Cryptography;
using System.Text;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class User
{
    /// <summary>Libellé de connexion : l'e-mail, sinon le nom d'utilisateur (`loginLabel`).</summary>
    public string LoginLabel => Email ?? Username ?? string.Empty;

    public bool HasRole(RoleName role) => Roles.Any(r => r.Role == role);

    /// <summary>Tout jeton émis avant <paramref name="now"/> devient caduc.</summary>
    public void RevokeTokens(DateTime now) => TokensValidFrom = now;

    public void ChangePassword(string passwordHash, bool mustChangePassword, DateTime now)
    {
        PasswordHash = passwordHash;
        MustChangePassword = mustChangePassword;
        RevokeTokens(now);
    }

    public void Deactivate(DateTime now)
    {
        IsActive = false;
        RevokeTokens(now);
    }

    public void SoftDelete(DateTime now)
    {
        DeletedAt = now;
        IsActive = false;
        RevokeTokens(now);
    }

    /// <summary>Ouverture d'un compte inscrit, à la validation de sa fiche (`openOnValidation`).</summary>
    public void OpenOnValidation(string? agencyId)
    {
        Status = UserStatus.ACTIVE;
        IsActive = true;
        MustChangePassword = false;
        if (agencyId is not null)
        {
            AgencyId = agencyId;
            if (Agencies.All(a => a.AgencyId != agencyId))
            {
                Agencies.Add(new UserAgency { UserId = Id, AgencyId = agencyId });
            }
        }
    }

    /// <summary>Refus d'une inscription (`closeOnRejection`).</summary>
    public void CloseOnRejection(DateTime now)
    {
        Status = UserStatus.REJECTED;
        IsActive = false;
        RevokeTokens(now);
    }

    /// <summary>
    /// Suppression par le titulaire : anonymisation, historique conservé.
    /// Rend l'adresse anonyme posée (reprise dans le journal d'audit).
    /// </summary>
    public string Anonymize(DateTime now)
    {
        var token = Guid.NewGuid().ToString();
        var previous = Email ?? Username ?? Id;
        var email = $"supprime+{token}@invalid.local";
        Email = email;
        Username = null;
        FirstName = "Compte";
        LastName = "supprimé";
        Phone = null;
        AvatarUrl = null;
        NationalId = null;
        JobTitle = null;
        PasswordHash = "deleted:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token + previous)));
        IsActive = false;
        DeletedAt = now;
        TokensValidFrom = now;
        return email;
    }
}
