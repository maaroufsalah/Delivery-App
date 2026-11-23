using System.Text.RegularExpressions;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.Identity.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Identity.Commands;

/// <summary>
/// `POST /public/register` : inscription publique d'un e-commerçant ou d'un
/// livreur, compte en attente de validation par un administrateur.
/// </summary>
public sealed record RegisterCommand : IRequest<RegistrationResult>
{
    public string? Kind { get; init; }

    public string? FirstName { get; init => field = Zod.Trim(value); }

    public string? LastName { get; init => field = Zod.Trim(value); }

    public string? Phone { get; init => field = value is null ? null : Phones.Normalize(value); }

    public string? CityId { get; init; }

    public string? Email { get; init; }

    public string? Password { get; init; }

    public string? ShopName { get; init => field = Zod.Trim(value); }

    public string? Username { get; init => field = Zod.TrimLower(value); }

    /// <summary>E-mail une fois transformé : rogné, en minuscules, vide → <c>null</c>.</summary>
    public string? NormalizedEmail => Zod.EmptyToNull(Zod.TrimLower(Email));
}

public sealed class RegisterValidator : ZodValidator<RegisterCommand>
{
    public static readonly Regex UsernamePattern = new("^[a-z0-9._-]{3,40}$", RegexOptions.Compiled);

    public RegisterValidator()
    {
        RuleFor(x => x.Kind).Must(k => k is "MERCHANT" or "COURIER").WithMessage(Zod.InvalidInput);
        When(x => x.Kind is "MERCHANT" or "COURIER", () =>
        {
            RuleFor(x => x.FirstName).Required().MinLength(2, "too-short").MaxLength(80);
            RuleFor(x => x.LastName).Required().MinLength(2, "too-short").MaxLength(80);
            RuleFor(x => x.Phone).Required().Pattern(Phones.Pattern, "invalid-phone");
            RuleFor(x => x.CityId).Required().MinLength(1, "required");
            RuleFor(x => x.Password).Required().MinLength(8, "password-too-short").MaxLength(200);
        });
        When(x => x.Kind == "MERCHANT", () =>
        {
            RuleFor(x => x.Email).Required().EmailAddress("invalid-email");
            RuleFor(x => x.ShopName).Required().MinLength(2, "too-short").MaxLength(120);
        });
        When(x => x.Kind == "COURIER", () =>
        {
            RuleFor(x => x.Email).Must(e => string.IsNullOrEmpty(e) || Zod.IsEmail(e)).WithMessage(Zod.InvalidInput);
            RuleFor(x => x.Username).Must(u => string.IsNullOrEmpty(u) || UsernamePattern.IsMatch(u)).WithMessage(Zod.InvalidInput);
        });
    }

    protected override void Refine(RegisterCommand value, Action<string, string> issue)
    {
        if (value.Kind == "COURIER" && value.NormalizedEmail is null && string.IsNullOrEmpty(value.Username))
        {
            issue("username", "email-or-username");
        }
    }
}

internal sealed class RegisterHandler(IAppDbContext db, ILogger<RegisterHandler> logger) : IRequestHandler<RegisterCommand, RegistrationResult>
{
    public async Task<RegistrationResult> Handle(RegisterCommand input, CancellationToken cancellationToken)
    {
        var merchant = input.Kind == "MERCHANT";
        var companyId = await Sessions.CompanyIdAsync(db, cancellationToken)
            ?? throw BusinessException.Internal("Inscription appelée hors contexte de société.");
        var open = await db.BoolSettingAsync(merchant ? SettingKeys.RegistrationMerchant : SettingKeys.RegistrationCourier, true, cancellationToken);
        if (!open) throw BusinessException.Forbidden("Les inscriptions sont fermées sur cet espace.");

        var email = input.NormalizedEmail;
        if (email is not null && await db.Users.AnyAsync(u => u.Email == email && u.DeletedAt == null, cancellationToken))
        {
            throw BusinessException.Conflict("Cette adresse est déjà utilisée sur cet espace.", new { field = "email" });
        }
        var username = merchant ? null : Zod.EmptyToNull(input.Username);
        if (username is not null && await db.Users.AnyAsync(u => u.Username == username, cancellationToken))
        {
            throw BusinessException.Conflict("Ce nom d’utilisateur est déjà pris sur cet espace.", new { field = "username" });
        }
        var cityExists = await db.Cities.AnyAsync(c => c.Id == input.CityId && c.IsActive && c.DeletedAt == null, cancellationToken);
        if (!cityExists) throw BusinessException.Validation("Ville inconnue.", new { field = "cityId" });

        var passwordHash = PasswordHasher.Hash(input.Password!);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = new User
        {
            Email = email,
            Username = username,
            PasswordHash = passwordHash,
            FirstName = input.FirstName!,
            LastName = input.LastName!,
            Phone = input.Phone,
            IsActive = false,
            Status = UserStatus.PENDING,
            MustChangePassword = false,
        };
        user.Roles.Add(new UserRole { UserId = user.Id, Role = merchant ? RoleName.MERCHANT : RoleName.COURIER });
        db.Users.Add(user);
        if (merchant)
        {
            db.Clients.Add(new Client
            {
                UserId = user.Id,
                Name = input.ShopName!,
                Type = ClientType.MERCHANT,
                Phone = input.Phone!,
                Email = email!,
                CityId = input.CityId!,
                AgencyId = await db.AgencyForCityAsync(input.CityId, cancellationToken),
                IsActive = false,
            });
        }
        else
        {
            db.Couriers.Add(new Courier
            {
                UserId = user.Id,
                FirstName = input.FirstName!,
                LastName = input.LastName!,
                Phone = input.Phone!,
                CityId = input.CityId!,
                IsActive = false,
            });
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Inscription {Kind} reçue pour {Login} (société {Company}).", input.Kind, email ?? username ?? "?", companyId);
        return new RegistrationResult(true, input.Kind!);
    }
}
