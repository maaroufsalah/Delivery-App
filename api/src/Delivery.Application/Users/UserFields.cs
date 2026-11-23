using System.Text.RegularExpressions;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Users;

/// <summary>Champs de `userFieldsSchema`, déjà transformés comme zod les rend.</summary>
public interface IUserFields
{
    string? Email { get; }

    string? Username { get; }

    string? FirstName { get; }

    string? LastName { get; }

    string? Phone { get; }

    string? JobTitle { get; }

    string? NationalId { get; }

    IReadOnlyList<string>? Roles { get; }

    string? AgencyId { get; }

    IReadOnlyList<string>? AgencyIds { get; }
}

internal static class UserFields
{
    public static readonly Regex UsernamePattern = new("^[a-z0-9._-]{3,40}$", RegexOptions.Compiled);

    public static readonly string[] RoleNames = Enum.GetNames<RoleName>();

    /// <summary>`optionalLoginEmailSchema` : e-mail valide sinon vide/absent ; minuscules ; vide → null.</summary>
    public static string? LoginEmail(string? raw) => Zod.EmptyToNull(Zod.TrimLower(raw));

    public static string? OptionalUsername(string? raw) => Zod.EmptyToNull(Zod.TrimLower(raw));

    public static void Apply<T>(AbstractValidator<T> validator)
        where T : IUserFields
    {
        validator.RuleFor(x => x.Email).Must(e => e is null || Zod.IsEmail(e)).WithMessage(Zod.InvalidInput);
        validator.RuleFor(x => x.Username).Must(u => u is null || UsernamePattern.IsMatch(u)).WithMessage(Zod.InvalidInput);
        validator.RuleFor(x => x.FirstName).Required().MinLength(2, "too-short").MaxLength(80);
        validator.RuleFor(x => x.LastName).Required().MinLength(2, "too-short").MaxLength(80);
        validator.RuleFor(x => x.Phone).Must(p => string.IsNullOrEmpty(p) || Phones.Pattern.IsMatch(p)).WithMessage(Zod.InvalidInput);
        validator.RuleFor(x => x.JobTitle).Must(v => v is null || v.Length <= 80).WithMessage(Zod.InvalidInput);
        validator.RuleFor(x => x.NationalId).Must(v => v is null || v.Length <= 30).WithMessage(Zod.InvalidInput);
        validator.RuleFor(x => x.Roles).Required("array").Must(r => r is null || r.Count >= 1).WithMessage("required");
        validator.RuleForEach(x => x.Roles).Must(r => RoleNames.Contains(r)).WithMessage(Zod.InvalidOption(RoleNames));
        validator.RuleForEach(x => x.AgencyIds).MinLength(1);
    }

    /// <summary>`requireLoginIdentity` : e-mail obligatoire, sauf livreur seul avec nom d'utilisateur.</summary>
    public static void RequireLoginIdentity(IUserFields value, Action<string, string> issue)
    {
        if (value.Email is not null) return;
        var courierOnly = value.Roles is { Count: 1 } && value.Roles[0] == "COURIER";
        if (courierOnly && value.Username is not null) return;
        issue("email", courierOnly ? "email-or-username" : "required");
    }

    /// <summary>Un rôle en double fait lever P2002 à Prisma (clé `userId, role`) : 409.</summary>
    public static void AssertDistinctRoles(IReadOnlyList<string> roles)
    {
        if (roles.Distinct(StringComparer.Ordinal).Count() != roles.Count)
        {
            throw BusinessException.Conflict("Cette valeur existe déjà.", new { fields = new[] { "userId", "role" } });
        }
    }

    public static List<string> AgencyList(string? agencyId, IReadOnlyList<string>? agencyIds) =>
        (string.IsNullOrEmpty(agencyId) ? [] : new List<string> { agencyId }).Concat(agencyIds ?? []).Distinct(StringComparer.Ordinal).ToList();

    public static async Task AssertAgenciesExistAsync(IAppDbContext db, List<string> agencyIds, CancellationToken cancellationToken)
    {
        if (agencyIds.Count == 0) return;
        var count = await db.Agencies.CountAsync(a => agencyIds.Contains(a.Id) && a.DeletedAt == null, cancellationToken);
        if (count != agencyIds.Count) throw BusinessException.Validation("Agence inconnue.", new { field = "agencyIds" });
    }

    public static async Task AssertEmailFreeAsync(IAppDbContext db, string? email, string? exceptId, CancellationToken cancellationToken)
    {
        if (email is null) return;
        var clash = await db.Users.AnyAsync(u => u.Email == email && u.DeletedAt == null && (exceptId == null || u.Id != exceptId), cancellationToken);
        if (clash) throw BusinessException.Conflict($"L’adresse {email} est déjà utilisée.", new { field = "email" });
    }

    public static async Task AssertUsernameFreeAsync(IAppDbContext db, string? username, string? exceptId, CancellationToken cancellationToken)
    {
        if (username is null) return;
        var clash = await db.Users.AnyAsync(u => u.Username == username && (exceptId == null || u.Id != exceptId), cancellationToken);
        if (clash) throw BusinessException.Conflict($"Le nom d’utilisateur {username} est déjà pris.", new { field = "username" });
    }

    /// <summary>`assertFicheRoles` : le compte d'une fiche livreur/client a ce rôle, et lui seul.</summary>
    public static void AssertFicheRoles(IReadOnlyList<string> roles, RoleName expected)
    {
        if (roles.Count == 1 && roles[0] == expected.ToString()) return;
        throw BusinessException.Validation(
            expected == RoleName.COURIER
                ? "Le compte d’une fiche livreur a le rôle Livreur, et lui seul."
                : "Le compte d’une fiche client a le rôle E-commerçant, et lui seul.",
            new { field = "roles", reason = "fiche-role" });
    }

    public static async Task AssertNotLastSuperAdminAsync(IAppDbContext db, string id, CancellationToken cancellationToken)
    {
        var isTargetSuperAdmin = await db.UserRoles.AnyAsync(r => r.UserId == id && r.Role == RoleName.SUPER_ADMIN, cancellationToken);
        if (!isTargetSuperAdmin) return;
        var remaining = await db.Users.CountAsync(
            u => u.DeletedAt == null && u.IsActive && u.Id != id && u.Roles.Any(r => r.Role == RoleName.SUPER_ADMIN), cancellationToken);
        if (remaining == 0)
        {
            throw BusinessException.Validation("C’est le dernier super-administrateur actif : désignez-en un autre d’abord.");
        }
    }

    private const string PasswordAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";

    public static string GeneratePassword()
    {
        var chars = new char[14];
        for (var i = 0; i < chars.Length; i++) chars[i] = PasswordAlphabet[System.Security.Cryptography.RandomNumberGenerator.GetInt32(PasswordAlphabet.Length)];
        return new string(chars);
    }
}
