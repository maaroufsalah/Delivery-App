using System.Net;
using System.Net.Http.Json;
using Delivery.Application.Common.Security;
using Delivery.ContractTests.Infrastructure;
using Delivery.Domain.Enums;
using Delivery.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.ContractTests.Referential;

/// <summary>
/// `import-laravel` sur un jeu Laravel synthétique (format de `scripts/laravel-dump-to-csv.py`) : à blanc par défaut,
/// idempotent, et les règles de Wimo Laravel reproduites (remise `enabled` NULL inactive, première ligne livreur + ville
/// tarifée, codes de ville en double, comptes partenaires non importés, empreinte bcrypt reprise puis refaite).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class LaravelImportContractTests(ApiFactory factory)
{
    /// <summary>`password_hash("Wimo-Laravel-2026!", PASSWORD_BCRYPT)` de PHP 7.4.</summary>
    private const string Bcrypt = "$2y$10$6.zqBSjhlL2CzvM7Vahv2ept8qxW7HjTIVFGaF9mUJMNEf2s94SWu";

    private const string N = "\\N";

    private static string Csv(params string[][] rows) => string.Join('\n', rows.Select(r => string.Join(',', r.Select(c => c.Contains(',') ? $"\"{c}\"" : c)))) + "\n";

    /// <summary>Le jeu du référentiel et des comptes, repris par le test de l'opérationnel.</summary>
    internal static Task<(string Folder, string Tag, string[] Codes)> FixtureFor(ApiFactory factory) => new LaravelImportContractTests(factory).FixtureAsync();

    private async Task<(string Folder, string Tag, string[] Codes)> FixtureAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var taken = await factory.QueryDbAsync(db => db.Cities.Select(c => c.Code).ToListAsync());
        var random = new Random();
        var codes = new List<string>();
        while (codes.Count < 3)
        {
            var code = new string(Enumerable.Range(0, 3).Select(_ => (char)('A' + random.Next(26))).ToArray());
            if (!taken.Contains(code) && !codes.Contains(code) && !codes.Any(c => c[0] == code[0])) codes.Add(code);
        }
        var folder = Path.Combine(Path.GetTempPath(), $"laravel-{tag}");
        Directory.CreateDirectory(folder);
        void W(string table, params string[][] rows) => File.WriteAllText(Path.Combine(folder, $"{table}.csv"), Csv(rows));
        W("zones", ["id", "label", "tarif", "villes", "created_at", "updated_at", "tarif_ville"], ["1", $"Zone {tag}", "35", "1,2", N, N, "20"]);
        // Ville 3 : même code que la 1, plus récente → code dérivé. Ville 4 : supprimée.
        W("villes", ["id", "label", "created_at", "updated_at", "code", "is_active", "legacy_id", "alentours", "deleted_at"],
            ["1", $"Alpha{tag}", "2022-01-01 10:00:00", N, codes[0], "1", N, N, N],
            ["2", $"Beta{tag}", "2022-01-02 10:00:00", N, codes[1], "1", N, N, N],
            ["3", $"Gamma{tag}", "2023-01-01 10:00:00", N, codes[0], "1", N, N, N],
            ["4", $"Delta{tag}", "2022-01-01 10:00:00", N, codes[2], "1", N, N, "2024-01-01 00:00:00"]);
        W("quartiers", ["id", "ville_id", "label", "created_at", "updated_at"], ["1", "1", "Maarif", N, N], ["2", "1", "maarif", N, N], ["3", "4", "Perdu", N, N]);
        W("zone_tarifs", ["id", "zone_from", "zone_to", "tarif", "delai"], ["1", "1", "1", "35", N], ["2", "1", "1", N, N]);
        W("tarifs", ["id", "depart_id", "destination_id", "prix", "prix_agence", "prix_quartier", "prix_point_relais", "enabled", "created_at", "updated_at", "delai", "depart_time"],
            ["1", "1", "2", N, "25", "30", N, "0", N, N, N, N], ["2", "1", "3", N, N, N, N, "1", N, N, N, N], ["3", N, "2", N, "40", "40", "40", "1", N, N, N, N]);
        W("agences", ["id", "label", "metier_id", "ville_id", "created_at", "updated_at", "image", "key", "tel", "email", "adresse", "latitude", "longitude", "alias", "deleted_at"],
            ["1", $"Agence {tag}", N, "1", N, N, N, N, "0522000000", N, "Rue 1", "33.5", "-7.6", N, N]);
        W("banques", ["id", "label", "logo", "created_at", "updated_at"], ["7", $"Banque {tag}", N, N, N]);
        W("compte_bancaires", ["id", "banque_id", "label", "rib", "created_at", "updated_at"], ["1", "7", $"Compte {tag}", "230 780 2488208211023300 43", N, N]);
        // Les valeurs par défaut du seed : le jeu ne change pas les réglages des autres tests.
        W("app_configs", ["id", "config_id", "value", "agence_id", "created_at", "updated_at"], ["1", "1", "0", N, N, N], ["2", "2", "10", N, N, N], ["3", "3", "20", N, N, N],
            ["4", "4", "5", N, N, N], ["5", "5", "1", N, N, N]);
        W("colis_statuts", ["id", "label", "color", "enable", "created_at", "updated_at", "icon"], ["4", "Livré", N, N, N, N, N], ["15", "En agence destination", N, N, N, N, N]);
        string[] User(string id, string email, string role, string? partner = null, string deleted = N) =>
            [id, "Nom", "Prenom", N, email, "0600000000", N, Bcrypt, N, "2022-01-01 10:00:00", "2022-01-01 10:00:00", N, "1", role, "1", "7", "230780248820821102330043", N, "1", N, N, "AB123", N, deleted, N,
                partner ?? N, N, N, N];
        W("users", ["id", "nom", "prenom", "surnom", "email", "tel", "email_verified_at", "password", "remember_token", "created_at", "updated_at", "image", "agence_id", "role_id", "ville_id",
                "banque_id", "rib", "client_id", "enabled", "code_retour", "password_retour", "cni", "fonction", "deleted_at", "acces_id", "partner_slug", "api_token", "api_token_previous", "agences"],
            User("10", $"admin-{tag}@laravel.test", "1"), User("11", $"staff-{tag}@laravel.test", "1"), User("12", $"vendeur-{tag}@laravel.test", "2"),
            User("13", $"livreur-{tag}@laravel.test", "4"), User("14", $"partner-{tag}@laravel.test", "1", "riyalto"), User("15", $"parti-{tag}@laravel.test", "2", deleted: "2024-01-01 00:00:00"));
        W("user_modules", ["id", "user_id", "module_id", "created_at", "updated_at"], ["1", "11", "1", N, N], ["2", "11", "13", N, N]);
        W("clients", ["id", "user_id", "agence_id", "created_at", "updated_at", "validation_user", "validation_date", "enabled", "type", "label", "ice", "sys_agence", "deleted_at", "prepare_colis",
            "frais_refus", "frais_retour"], ["5", "12", "1", "2022-01-01 10:00:00", N, N, N, "1", "ecommercant", $"Boutique {tag}", N, "1", N, N, N, N]);
        W("client_remises", ["id", "client_id", "zone_id", "meme_ville", "frais_livraison", "frais_ramassage", "enabled", "created_at", "updated_at"],
            ["1", "5", "1", "15", "25", N, N, N, N]);
        W("client_ramassage_adresses", ["id", "client_id", "ville_id", "ramassage_id", "ramassage_type", "adresse", "position", "created_at", "updated_at"],
            ["1", "5", "1", "1", "App\\Quartier", "Rue du dépôt", N, N, N], ["2", "5", "1", N, "App\\Quartier", "Rue du dépôt", N, N, N]);
        W("livreurs", ["id", "user_id", "enabled", "created_at", "updated_at", "ville_id", "sys_agence", "validation_user", "validation_date", "deleted_at", "vehicule_type_id"],
            ["8", "13", "1", "2022-01-01 10:00:00", N, "1", "1", N, N, N, "2"]);
        // Paire livreur + ville 1 : la première ligne n'a pas de frais (ne compte pas), la deuxième gagne, la troisième est un doublon.
        W("livreur_ville", ["id", "livreur_id", "ville_id", "frais_ramassage", "frais_livraison"], ["1", "8", "1", "5", N], ["2", "8", "1", "4", "15"], ["3", "8", "1", "9", "99"]);
        return (folder, tag, codes.ToArray());
    }

    private async Task<LaravelImporter.Report> ImportAsync(string folder, bool apply)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<LaravelImporter>().ImportAsync(folder, apply);
    }

    [Fact]
    public async Task Imports_the_Laravel_referential_and_accounts_dry_run_first_then_idempotently()
    {
        // Les identifiants Laravel du jeu reviennent d'un test à l'autre : chaque test part d'une correspondance vide.
        await factory.ExecuteDbAsync(db => db.LegacyMappings.ExecuteDeleteAsync());
        var (folder, tag, codes) = await FixtureAsync();

        var dry = await ImportAsync(folder, apply: false);
        dry.Applied.ShouldBeFalse();
        dry.Tables.Single(t => t.Table == "users").Imported.ShouldBe(4);
        (await factory.QueryDbAsync(db => db.Users.CountAsync(u => u.Email!.EndsWith($"-{tag}@laravel.test")))).ShouldBe(0);
        (await factory.QueryDbAsync(db => db.Zones.CountAsync(z => z.Name == $"Zone {tag}"))).ShouldBe(0);

        var report = await ImportAsync(folder, apply: true);
        var users = report.Tables.Single(t => t.Table == "users");
        users.Read.ShouldBe(6);
        users.Imported.ShouldBe(4);
        users.Ignored.Keys.ShouldContain("compte partenaire API (listé, non importé)");
        users.Ignored.Keys.ShouldContain("compte supprimé dans Laravel");
        report.Tables.Single(t => t.Table == "colis_statuts").Ignored.Values.Single().ShouldBe(["15 En agence destination"]);

        var cities = await factory.QueryDbAsync(db => db.Cities.Where(c => c.Name.EndsWith(tag)).Select(c => new { c.Name, c.Code, Zone = c.Zone.Name }).ToListAsync());
        cities.Count.ShouldBe(3);
        cities.Single(c => c.Name.StartsWith("Alpha")).Code.ShouldBe(codes[0]);
        cities.Single(c => c.Name.StartsWith("Gamma")).Code.ShouldNotBe(codes[0]);
        cities.Single(c => c.Name.StartsWith("Gamma")).Zone.ShouldBe(LaravelImporter.OutOfZone);
        cities.Single(c => c.Name.StartsWith("Beta")).Zone.ShouldBe($"Zone {tag}");
        (await factory.QueryDbAsync(db => db.Districts.CountAsync(d => d.City.Name == $"Alpha{tag}"))).ShouldBe(1);

        var rule = await factory.QueryDbAsync(db => db.PricingRules.Where(r => r.OriginCity.Name == $"Alpha{tag}").ToListAsync());
        rule.Count.ShouldBe(1);
        rule[0].RateDistrict.ShouldBe(30m);
        rule[0].RateAgency.ShouldBe(25m);
        rule[0].RateRelayPoint.ShouldBe(0m);
        rule[0].IsEnabled.ShouldBeTrue();
        (await factory.QueryDbAsync(db => db.BankAccounts.Where(a => a.Label == $"Compte {tag}").Select(a => a.Rib).SingleAsync())).ShouldBe("230780248820821102330043");

        var roles = await factory.QueryDbAsync(db => db.Users.Where(u => u.Email!.EndsWith($"-{tag}@laravel.test"))
            .Select(u => new { u.Email, Role = u.Roles.Select(r => r.Role).Single(), u.PasswordHash, u.AgencyId }).ToListAsync());
        roles.Single(u => u.Email!.StartsWith("admin-")).Role.ShouldBe(RoleName.SUPER_ADMIN);
        roles.Single(u => u.Email!.StartsWith("staff-")).Role.ShouldBe(RoleName.STAFF);
        roles.Single(u => u.Email!.StartsWith("staff-")).AgencyId.ShouldNotBeNull();
        roles.ShouldAllBe(u => u.PasswordHash == Bcrypt);

        var client = await factory.QueryDbAsync(db => db.Clients.Where(c => c.Name == $"Boutique {tag}").Select(c => new { c.Id, c.Type, c.ValidatedAt, c.IsActive }).SingleAsync());
        client.Type.ShouldBe(ClientType.MERCHANT);
        client.ValidatedAt.ShouldNotBeNull();
        var discount = await factory.QueryDbAsync(db => db.ClientDiscounts.SingleAsync(d => d.ClientId == client.Id));
        discount.IsActive.ShouldBeFalse();
        discount.FlatRate.ShouldBe(25m);
        discount.SameCityRate.ShouldBe(15m);
        (await factory.QueryDbAsync(db => db.ClientPickupAddresses.CountAsync(a => a.ClientId == client.Id && a.DistrictId != null))).ShouldBe(1);

        var courier = await factory.QueryDbAsync(db => db.Couriers.Where(c => c.User!.Email == $"livreur-{tag}@laravel.test")
            .Select(c => new { c.VehicleType, c.Rib, Cities = db.CourierCities.Where(l => l.CourierId == c.Id).Select(l => new { l.DeliveryFee, l.PickupFee }).ToList() }).SingleAsync());
        courier.VehicleType.ShouldBe(VehicleType.CAR);
        courier.Rib.ShouldBe("230780248820821102330043");
        courier.Cities.Count.ShouldBe(1);
        courier.Cities[0].DeliveryFee.ShouldBe(15m);
        courier.Cities[0].PickupFee.ShouldBe(4m);

        // Rejeu : rien ne double ; un mot de passe refait en argon2id n'est jamais écrasé par le bcrypt du dump.
        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { identifier = $"vendeur-{tag}@laravel.test", password = "Wimo-Laravel-2026!" });
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ImportAsync(folder, apply: true);
        (await factory.QueryDbAsync(db => db.Cities.CountAsync(c => c.Name.EndsWith(tag)))).ShouldBe(3);
        (await factory.QueryDbAsync(db => db.Users.CountAsync(u => u.Email!.EndsWith($"-{tag}@laravel.test")))).ShouldBe(4);
        (await factory.QueryDbAsync(db => db.ClientDiscounts.CountAsync(d => d.ClientId == client.Id))).ShouldBe(1);
        var hash = await factory.QueryDbAsync(db => db.Users.Where(u => u.Email == $"vendeur-{tag}@laravel.test").Select(u => u.PasswordHash).SingleAsync());
        PasswordHasher.NeedsRehash(hash).ShouldBeFalse();
        Directory.Delete(folder, true);
    }
}
