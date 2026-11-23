using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Delivery.Infrastructure.Persistence.Seed;

/// <summary>
/// Seed idempotent : la société Wimo (valeurs du seed Speedy, `cmp-wimo`), le
/// catalogue des permissions, et un super-administrateur si `SEED_ADMIN_*` est
/// fourni. Un rejeu n'écrase jamais un réglage déjà posé.
/// </summary>
public sealed class DatabaseSeeder(AppDbContext db, IClock clock, IConfiguration configuration, ILogger<DatabaseSeeder> logger)
{
    public const string CompanyId = "cmp-wimo";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedCompanyAsync(cancellationToken);
        await SeedPermissionsAsync(cancellationToken);
        await SeedAdminAsync(cancellationToken);
    }

    private async Task SeedCompanyAsync(CancellationToken cancellationToken)
    {
        if (await db.Companies.AnyAsync(cancellationToken)) return;
        db.Companies.Add(new Company
        {
            Id = CompanyId,
            Slug = "wimo",
            Name = "Wimo",
            LegalName = "Wimo Express SARL",
            TaxId = "001987654000032",
            Rc = "RC 98211 Rabat",
            Address = "8 avenue Fal Ould Oumeir, Agdal",
            City = "Rabat",
            Phone = "+212 537 77 20 41",
            Email = "contact@wimo.ma",
            Website = "https://wimo.ma",
            Status = CompanyStatus.ACTIVE,
            Country = "MA",
            Currency = "MAD",
            Timezone = "Africa/Casablanca",
            DefaultLocale = "fr",
            Locales = ["fr", "ar", "en"],
            LogoUrl = "/brands/wimo.svg",
            PrimaryColor = "#2F6FED",
            SecondaryColor = "#12306B",
        });
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seed : société Wimo créée.");
    }

    private async Task SeedPermissionsAsync(CancellationToken cancellationToken)
    {
        var existing = await db.Permissions.ToDictionaryAsync(p => p.Key, cancellationToken);
        foreach (var permission in PermissionCatalog.All)
        {
            if (existing.TryGetValue(permission.Key, out var row)) row.Label = permission.Label;
            else db.Permissions.Add(new Permission { Id = permission.Key, Key = permission.Key, Label = permission.Label });
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedAdminAsync(CancellationToken cancellationToken)
    {
        var email = configuration["SEED_ADMIN_EMAIL"]?.Trim().ToLowerInvariant();
        var password = configuration["SEED_ADMIN_PASSWORD"];
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password)) return;
        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken)) return;
        var user = new User
        {
            Email = email,
            PasswordHash = PasswordHasher.Hash(password),
            FirstName = "Admin",
            LastName = "Wimo",
            Status = UserStatus.ACTIVE,
            TokensValidFrom = clock.UtcNow,
        };
        user.Roles.Add(new UserRole { UserId = user.Id, Role = RoleName.SUPER_ADMIN });
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seed : super-administrateur {Email} créé.", email);
    }
}
