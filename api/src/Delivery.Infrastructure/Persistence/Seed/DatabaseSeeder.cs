using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using System.Text.Json.Nodes;
using Delivery.Application.Settings;
using Delivery.Domain.Identity;
using Delivery.Domain.Parcels;
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
        await SeedStatusesAsync(cancellationToken);
        await SeedSettingsAsync(cancellationToken);
        await SeedReferentialAsync(cancellationToken);
        await SeedAdminAsync(cancellationToken);
    }

    /// <summary>Statuts et machine à états, dérivés du catalogue ; transitions retirées effacées.</summary>
    private async Task SeedStatusesAsync(CancellationToken cancellationToken)
    {
        var existing = await db.ParcelStatuses.ToDictionaryAsync(s => s.Id, cancellationToken);
        for (var index = 0; index < StatusCatalog.All.Count; index++)
        {
            var status = StatusCatalog.All[index];
            if (!existing.TryGetValue(status.Id, out var row))
            {
                row = new ParcelStatus { Id = status.Id };
                db.ParcelStatuses.Add(row);
            }
            row.Key = status.Key;
            row.Group = Enum.Parse<ParcelStatusGroup>(status.Group.ToUpperInvariant());
            row.Color = status.Color;
            row.Icon = status.Icon;
            row.SortOrder = index;
        }
        await db.SaveChangesAsync(cancellationToken);

        var wanted = StatusCatalog.TransitionRows().Select(t => (t.From, t.To, Kind: Enum.Parse<TransitionKind>(t.Kind))).Distinct().ToHashSet();
        var current = await db.ParcelTransitions.ToListAsync(cancellationToken);
        foreach (var stale in current.Where(t => !wanted.Contains((t.FromStatusId, t.ToStatusId, t.Kind)))) db.ParcelTransitions.Remove(stale);
        var have = current.Select(t => (t.FromStatusId, t.ToStatusId, t.Kind)).ToHashSet();
        foreach (var (from, to, kind) in wanted.Where(w => !have.Contains(w)))
        {
            db.ParcelTransitions.Add(new ParcelTransition { FromStatusId = from, ToStatusId = to, Kind = kind });
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Paramètres par défaut du seed Speedy pour Wimo ; jamais réécrits s'ils existent.</summary>
    private async Task SeedSettingsAsync(CancellationToken cancellationToken)
    {
        var defaults = new (string Key, JsonNode Value)[]
        {
            ("pricing.failureFee.sameCity", 0),
            ("pricing.failureFee.crossCity", 1000),
            ("pricing.pickupFee", 2000),
            ("pricing.freePickupThreshold", 5),
            ("locales.enabled", new JsonArray("fr", "ar", "en")),
            ("locales.default", "fr"),
            ("claims.resolutionDays", 5),
            ("finance.courierFailedCommission", 0),
            ("theme.presets", BrandPalettes.Defaults()),
            ("theme.standard", new JsonObject { ["primaryColor"] = "#F7941E", ["secondaryColor"] = "#164E9B", ["theme"] = new JsonObject() }),
        };
        var ids = defaults.Select(d => $"{CompanyId}:company:{d.Key}").ToList();
        var existing = await db.Settings.Where(s => ids.Contains(s.Id)).Select(s => s.Id).ToListAsync(cancellationToken);
        foreach (var (key, value) in defaults)
        {
            var id = $"{CompanyId}:company:{key}";
            if (existing.Contains(id)) continue;
            db.Settings.Add(new Setting { Id = id, Scope = SettingScope.COMPANY, Key = key, Value = value.ToJsonString() });
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Référentiel minimal (la reprise réelle passe par `scripts/import-referential`) :
    /// type « Livraison normale », zone, ville et agence de Casablanca.
    /// </summary>
    private async Task SeedReferentialAsync(CancellationToken cancellationToken)
    {
        if (!await db.DeliveryTypes.AnyAsync(cancellationToken))
        {
            db.DeliveryTypes.Add(new DeliveryType { Id = "dlt-standard", Name = "Livraison normale", Multiplier = 1m, IsDefault = true });
        }
        if (!await db.Cities.AnyAsync(cancellationToken))
        {
            var zone = new Zone { Id = "zon-casablanca", Name = "Casablanca", BaseRate = 40m, SameCityRate = 20m, DeliveryDays = 1 };
            var city = new City { Id = "cty-casablanca", ZoneId = zone.Id, Name = "Casablanca", NameAr = "الدار البيضاء", Code = "CAS" };
            db.Zones.Add(zone);
            db.Cities.Add(city);
            db.ZoneCities.Add(new ZoneCity { ZoneId = zone.Id, CityId = city.Id });
            if (!await db.Agencies.AnyAsync(cancellationToken))
            {
                db.Agencies.Add(new Agency { Id = "agc-casablanca", Name = "Wimo Casablanca", CityId = city.Id });
            }
        }
        await db.SaveChangesAsync(cancellationToken);
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
