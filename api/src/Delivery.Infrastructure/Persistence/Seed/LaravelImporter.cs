using System.Globalization;
using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Infrastructure.Persistence.Seed;

/// <summary>
/// Reprise de Wimo Laravel (référentiel et comptes, ni colis ni données financières) depuis le dossier de CSV produit par
/// `scripts/laravel-dump-to-csv.py` (une table Laravel par fichier, `\N` = NULL). À BLANC par défaut : tout s'écrit dans
/// une transaction annulée à la fin, le compte rendu donne les vrais nombres ; `apply` la valide. Idempotent : clés
/// naturelles (nom de zone, code de ville, ville + quartier, nom d'agence, paire de villes, e-mail, compte du vendeur ou
/// du livreur) ; un rejeu met à jour, ne duplique pas, et n'écrase jamais le mot de passe d'un compte existant.
/// Décisions de reprise : `docs/chantiers/reprise-wimo-laravel.md`.
/// </summary>
public sealed partial class LaravelImporter(AppDbContext db)
{
    public const string OutOfZone = "Hors zone";
    private const string Null = "\\N";

    /// <summary>Les 17 droits « feuilles » de `app_modules` : tous ou aucun = administrateur, sinon personnel de son agence.</summary>
    private const int AllModules = 17;

    public sealed class TableReport(string table)
    {
        public string Table { get; } = table;
        public int Read { get; set; }
        public int Imported { get; set; }
        public int Unchanged { get; set; }
        public int Updated { get; set; }
        public Dictionary<string, List<string>> Ignored { get; } = [];
        public List<string> Notes { get; } = [];

        public int IgnoredCount => Ignored.Values.Sum(v => v.Count);

        public void Ignore(string reason, string id)
        {
            if (!Ignored.TryGetValue(reason, out var ids)) Ignored[reason] = ids = [];
            ids.Add(id);
        }
    }

    public sealed record Report(bool Applied, IReadOnlyList<TableReport> Tables);

    /// <summary>`BatchSize` : lignes par lot (une transaction chacun en mode `apply`).</summary>
    public sealed record ImportOptions(bool Apply, int BatchSize = 500);

    private readonly Dictionary<string, BankAccount> bankAccounts = [];

    private sealed class Source(string folder)
    {
        public bool Has(string table) => File.Exists(Path.Combine(folder, $"{table}.csv"));

        public List<Dictionary<string, string?>> Rows(string table)
        {
            var rows = ReferentialImporter.Csv.Read(Path.Combine(folder, $"{table}.csv"))
                       ?? throw new FileNotFoundException($"Table Laravel absente du dossier : {table}.csv");
            var header = rows.Count == 0 ? [] : Header(table);
            return rows.Select(r => header.ToDictionary(h => h, h => r.Get(h) is { Length: > 0 } v && v != Null ? v : null)).ToList();
        }

        private List<string> Header(string table)
        {
            using var reader = new StreamReader(Path.Combine(folder, $"{table}.csv"));
            return (reader.ReadLine() ?? string.Empty).TrimStart('﻿').Split(',').Select(h => h.Trim()).ToList();
        }
    }

    private static string? V(Dictionary<string, string?> row, string column) => row.TryGetValue(column, out var v) ? v?.Trim() is { Length: > 0 } t ? t : null : null;

    private static bool Deleted(Dictionary<string, string?> row) => V(row, "deleted_at") is not null;

    /// <summary>Un RIB Laravel saisi avec espaces : 24 chiffres côté .NET, sinon `null` (le RIB brut est listé au compte rendu).</summary>
    private static string? Rib(string? value, TableReport report, string who)
    {
        if (value is null) return null;
        var digits = new string(value.Where(char.IsAsciiLetterOrDigit).ToArray());
        if (digits.Length is > 0 and <= 24) return digits;
        report.Notes.Add($"{who} : RIB « {value} » illisible ({digits.Length} caractères, 24 au plus) — non repris.");
        return null;
    }

    private static decimal? Mad(string? value) =>
        value is not null && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? Math.Round(d, 2) : null;

    /// <summary>Les horodatages Laravel sont l'heure locale de Casablanca, sans fuseau.</summary>
    private static DateTime? At(string? value)
    {
        if (value is null || !DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)) return null;
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), ClockDays.Zone);
    }

    public Task<Report> ImportAsync(string folder, bool apply, CancellationToken cancellationToken = default) =>
        ImportAsync(folder, new ImportOptions(apply), cancellationToken);

    /// <summary>
    /// Référentiel et comptes (une transaction), puis l'opérationnel et l'argent par lots (une transaction par lot, reprise
    /// possible après une coupure : `legacy_mappings` dit ce qui est fait, l'empreinte ce qui a changé). À blanc : tout
    /// dans une seule transaction annulée à la fin.
    /// </summary>
    public async Task<Report> ImportAsync(string folder, ImportOptions options, CancellationToken cancellationToken = default)
    {
        var source = new Source(folder);
        var tables = new List<TableReport>();
        await using var dryRun = options.Apply ? null : await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await AuditSilence.RunAsync(async () =>
            {
                await InBatchAsync(options, async () =>
                {
                    var zones = await ZonesAsync(source, tables, cancellationToken);
                    var cities = await CitiesAsync(source, zones, tables, cancellationToken);
                    var districts = await DistrictsAsync(source, cities, tables, cancellationToken);
                    await ZoneRatesAsync(source, zones, tables, cancellationToken);
                    await PricingAsync(source, cities, tables, cancellationToken);
                    var agencies = await AgenciesAsync(source, cities, tables, cancellationToken);
                    var banks = await BanksAsync(source, tables, cancellationToken);
                    await SettingsAsync(source, tables, cancellationToken);
                    StatusesReport(source, tables);
                    var users = await UsersAsync(source, agencies, tables, cancellationToken);
                    var clients = await ClientsAsync(source, users, cities, agencies, tables, cancellationToken);
                    await DiscountsAsync(source, clients, zones, tables, cancellationToken);
                    await PickupAddressesAsync(source, clients, cities, districts, users, tables, cancellationToken);
                    var couriers = await CouriersAsync(source, users, cities, agencies, banks, tables, cancellationToken);
                    await CourierCitiesAsync(source, couriers, cities, tables, cancellationToken);
                    await ClaimTypesAsync(source, tables, cancellationToken);
                    await MapReferentialAsync(source, zones, cities, districts, agencies, banks, users, clients, couriers, cancellationToken);
                }, cancellationToken);
                await OperationsAsync(source, options, tables, cancellationToken);
            });
        }
        finally
        {
            if (dryRun is not null) await dryRun.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }
        return new Report(options.Apply, tables);
    }

    /// <summary>Un lot : sa propre transaction validée en mode `apply` ; à blanc, il s'écrit dans la transaction annulée.</summary>
    private async Task InBatchAsync(ImportOptions options, Func<Task> work, CancellationToken cancellationToken)
    {
        if (!options.Apply)
        {
            await work();
            await db.SaveChangesAsync(cancellationToken);
            return;
        }
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await work();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    private async Task<Dictionary<string, Zone>> ZonesAsync(Source source, List<TableReport> tables, CancellationToken cancellationToken)
    {
        var report = Add(tables, "zones");
        var existing = await db.Zones.Where(z => z.DeletedAt == null).ToListAsync(cancellationToken);
        var map = new Dictionary<string, Zone>();
        foreach (var row in source.Rows("zones"))
        {
            report.Read++;
            var name = V(row, "label")!;
            var zone = existing.FirstOrDefault(z => string.Equals(z.Name, name, StringComparison.OrdinalIgnoreCase));
            if (zone is null)
            {
                zone = new Zone { Name = name };
                db.Zones.Add(zone);
                existing.Add(zone);
            }
            zone.BaseRate = Mad(V(row, "tarif")) ?? 0m;
            // `tarif_ville` NULL : Laravel n'a pas de prix « même ville » pour la zone, le prix de la zone s'applique.
            zone.SameCityRate = Mad(V(row, "tarif_ville")) ?? zone.BaseRate;
            map[V(row, "id")!] = zone;
            report.Imported++;
        }
        var outside = existing.FirstOrDefault(z => string.Equals(z.Name, OutOfZone, StringComparison.OrdinalIgnoreCase));
        if (outside is null)
        {
            outside = new Zone { Name = OutOfZone, BaseRate = 0m, SameCityRate = 0m };
            db.Zones.Add(outside);
        }
        map[OutOfZone] = outside;
        report.Notes.Add($"zone « {OutOfZone} » créée pour les villes qu'aucune zone Laravel ne contient (aucune remise de zone ne s'y applique, comme dans Laravel).");
        await db.SaveChangesAsync(cancellationToken);
        return map;
    }

    private async Task<Dictionary<string, City>> CitiesAsync(Source source, Dictionary<string, Zone> zones, List<TableReport> tables, CancellationToken cancellationToken)
    {
        var report = Add(tables, "villes");
        var zoneOf = new Dictionary<string, string>();
        foreach (var row in source.Rows("zones"))
        {
            foreach (var id in (V(row, "villes") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                zoneOf.TryAdd(id, V(row, "id")!);
            }
        }
        var rows = source.Rows("villes").OrderBy(r => At(V(r, "created_at")) ?? DateTime.MaxValue).ThenBy(r => int.Parse(V(r, "id")!, CultureInfo.InvariantCulture)).ToList();
        var existing = await db.Cities.Include(c => c.ZoneLinks).ToListAsync(cancellationToken);
        // Codes dérivés tirés des SEULES données Laravel (tous les codes d'origine réservés) : un rejeu retrouve les mêmes.
        var used = rows.Select(r => (V(r, "code") ?? string.Empty).ToUpperInvariant()).ToHashSet();
        var claimed = new Dictionary<string, string>();
        var map = new Dictionary<string, City>();
        var outside = 0;
        foreach (var row in rows)
        {
            report.Read++;
            var id = V(row, "id")!;
            var label = V(row, "label") ?? string.Empty;
            if (Deleted(row))
            {
                report.Ignore("ville supprimée dans Laravel", $"{id} {label}");
                continue;
            }
            var code = (V(row, "code") ?? string.Empty).ToUpperInvariant();
            if (claimed.TryGetValue(code, out var holder))
            {
                var derived = DerivedCode(label, code, used, claimed);
                report.Notes.Add($"code {code} en double : {holder} le garde (la plus ancienne), {id} {label} prend {derived}.");
                code = derived;
            }
            claimed[code] = $"{id} {label}";
            var city = existing.FirstOrDefault(c => c.Code == code);
            if (city is null)
            {
                city = new City { Code = code, CreatedAt = At(V(row, "created_at")) ?? DateTime.UtcNow };
                db.Cities.Add(city);
                existing.Add(city);
            }
            var (clean, arabic) = CityNames.Normalize(label);
            city.Name = clean;
            city.NameAr ??= arabic;
            var zone = zoneOf.TryGetValue(id, out var zoneId) && zones.TryGetValue(zoneId, out var z) ? z : zones[OutOfZone];
            if (zone == zones[OutOfZone]) outside++;
            city.ZoneId = zone.Id;
            if (city.ZoneLinks.All(l => l.ZoneId != zone.Id)) city.ZoneLinks.Add(new ZoneCity { ZoneId = zone.Id, CityId = city.Id });
            city.IsActive = V(row, "is_active") != "0";
            city.DeletedAt = null;
            map[id] = city;
            report.Imported++;
        }
        report.Notes.Add($"{outside} ville(s) importée(s) dans « {OutOfZone} » : aucune zone Laravel ne les contient (`tarifs` est ville → ville, sans zone).");
        await db.SaveChangesAsync(cancellationToken);
        return map;
    }

    /// <summary>Un code de 3 lettres libre, tiré des lettres du nom (première + deux suivantes), puis de l'alphabet.</summary>
    private static string DerivedCode(string label, string code, HashSet<string> used, Dictionary<string, string> claimed)
    {
        var letters = new string(label.ToUpperInvariant().Normalize(System.Text.NormalizationForm.FormD).Where(char.IsAsciiLetterUpper).ToArray());
        bool Free(string c) => c != code && !used.Contains(c) && !claimed.ContainsKey(c);
        for (var i = 1; i < letters.Length; i++)
        {
            for (var j = i + 1; j < letters.Length; j++)
            {
                var candidate = $"{letters[0]}{letters[i]}{letters[j]}";
                if (Free(candidate)) return candidate;
            }
        }
        for (var a = 'A'; a <= 'Z'; a++)
        {
            for (var b = 'A'; b <= 'Z'; b++)
            {
                var candidate = $"{code[0]}{a}{b}";
                if (Free(candidate)) return candidate;
            }
        }
        throw new InvalidOperationException($"Aucun code libre pour {label}.");
    }

    private async Task<Dictionary<string, District>> DistrictsAsync(Source source, Dictionary<string, City> cities, List<TableReport> tables, CancellationToken cancellationToken)
    {
        var report = Add(tables, "quartiers");
        var existing = (await db.Districts.Where(d => d.DeletedAt == null).ToListAsync(cancellationToken)).GroupBy(d => (d.CityId, CityNames.DistrictKey(d.Name)))
            .ToDictionary(g => g.Key, g => g.First());
        var map = new Dictionary<string, District>();
        foreach (var row in source.Rows("quartiers"))
        {
            report.Read++;
            var id = V(row, "id")!;
            if (V(row, "ville_id") is not { } cityId || !cities.TryGetValue(cityId, out var city))
            {
                report.Ignore("ville absente ou supprimée", id);
                continue;
            }
            var name = V(row, "label") ?? string.Empty;
            if (name.Length < 2)
            {
                report.Ignore("nom de moins de 2 caractères", id);
                continue;
            }
            var key = (city.Id, CityNames.DistrictKey(name));
            if (!existing.TryGetValue(key, out var district))
            {
                district = new District { CityId = city.Id, Name = name };
                db.Districts.Add(district);
                existing[key] = district;
                report.Imported++;
            }
            else if (map.ContainsValue(district))
            {
                report.Ignore("doublon dans la même ville (même nom)", id);
            }
            else
            {
                report.Imported++;
            }
            map[id] = district;
        }
        await db.SaveChangesAsync(cancellationToken);
        return map;
    }

    private async Task ZoneRatesAsync(Source source, Dictionary<string, Zone> zones, List<TableReport> tables, CancellationToken cancellationToken)
    {
        var report = Add(tables, "zone_tarifs");
        var existing = await db.ZoneRates.ToListAsync(cancellationToken);
        foreach (var row in source.Rows("zone_tarifs"))
        {
            report.Read++;
            var id = V(row, "id")!;
            if (Mad(V(row, "tarif")) is not { } rate)
            {
                report.Ignore("aucun tarif", id);
                continue;
            }
            if (!zones.TryGetValue(V(row, "zone_from") ?? "", out var from) || !zones.TryGetValue(V(row, "zone_to") ?? "", out var to))
            {
                report.Ignore("zone inconnue", id);
                continue;
            }
            var line = existing.FirstOrDefault(r => r.FromZoneId == from.Id && r.ToZoneId == to.Id);
            if (line is null)
            {
                line = new ZoneRate { FromZoneId = from.Id, ToZoneId = to.Id };
                db.ZoneRates.Add(line);
                existing.Add(line);
            }
            line.Rate = rate;
            if (int.TryParse(V(row, "delai"), CultureInfo.InvariantCulture, out var days)) line.DeliveryDays = days;
            line.IsActive = true;
            report.Imported++;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// `tarifs` ville → ville. Laravel lit la paire sans regarder `enabled` (`calculeFraisLivraison`) : une règle reprise est
    /// active. Un prix NULL vaut 0 dans le calcul Laravel (`+= null`) ; une ligne sans aucun prix est ignorée (décision 5).
    /// </summary>
    private async Task PricingAsync(Source source, Dictionary<string, City> cities, List<TableReport> tables, CancellationToken cancellationToken)
    {
        var report = Add(tables, "tarifs");
        var existing = (await db.PricingRules.ToListAsync(cancellationToken)).ToDictionary(r => (r.OriginCityId, r.DestinationCityId));
        foreach (var row in source.Rows("tarifs"))
        {
            report.Read++;
            var id = V(row, "id")!;
            if (V(row, "depart_id") is not { } departId)
            {
                report.Ignore("sans ville de départ", id);
                continue;
            }
            var district = Mad(V(row, "prix_quartier"));
            var agency = Mad(V(row, "prix_agence"));
            var relay = Mad(V(row, "prix_point_relais"));
            if (district is null && agency is null && relay is null)
            {
                report.Ignore("aucun prix", id);
                continue;
            }
            if (!cities.TryGetValue(departId, out var origin) || !cities.TryGetValue(V(row, "destination_id") ?? "", out var destination))
            {
                report.Ignore("ville de départ ou d'arrivée supprimée", id);
                continue;
            }
            if (!existing.TryGetValue((origin.Id, destination.Id), out var rule))
            {
                rule = new PricingRule { OriginCityId = origin.Id, DestinationCityId = destination.Id };
                db.PricingRules.Add(rule);
                existing[(origin.Id, destination.Id)] = rule;
            }
            rule.RateDistrict = district ?? 0m;
            rule.RateAgency = agency ?? 0m;
            rule.RateRelayPoint = relay ?? 0m;
            rule.IsEnabled = true;
            rule.DeletedAt = null;
            report.Imported++;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Dictionary<string, Agency>> AgenciesAsync(Source source, Dictionary<string, City> cities, List<TableReport> tables, CancellationToken cancellationToken)
    {
        var report = Add(tables, "agences");
        var existing = await db.Agencies.Where(a => a.DeletedAt == null).ToListAsync(cancellationToken);
        var map = new Dictionary<string, Agency>();
        foreach (var row in source.Rows("agences"))
        {
            report.Read++;
            var id = V(row, "id")!;
            var name = V(row, "label") ?? string.Empty;
            if (Deleted(row))
            {
                report.Ignore("agence supprimée", $"{id} {name}");
                continue;
            }
            if (!cities.TryGetValue(V(row, "ville_id") ?? "", out var city))
            {
                report.Ignore("ville inconnue", $"{id} {name}");
                continue;
            }
            var agency = existing.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
            if (agency is null)
            {
                agency = new Agency { Name = name };
                db.Agencies.Add(agency);
                existing.Add(agency);
            }
            agency.CityId = city.Id;
            agency.Phone = V(row, "tel") ?? agency.Phone;
            agency.Email = V(row, "email") ?? agency.Email;
            agency.Address = V(row, "adresse") ?? agency.Address;
            agency.Latitude = decimal.TryParse(V(row, "latitude"), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ? lat : agency.Latitude;
            agency.Longitude = decimal.TryParse(V(row, "longitude"), NumberStyles.Float, CultureInfo.InvariantCulture, out var lng) ? lng : agency.Longitude;
            map[id] = agency;
            report.Imported++;
        }
        await db.SaveChangesAsync(cancellationToken);
        return map;
    }

    private async Task<Dictionary<string, Bank>> BanksAsync(Source source, List<TableReport> tables, CancellationToken cancellationToken)
    {
        var report = Add(tables, "banques");
        var existing = await db.Banks.Where(b => b.DeletedAt == null).ToListAsync(cancellationToken);
        var map = new Dictionary<string, Bank>();
        foreach (var row in source.Rows("banques"))
        {
            report.Read++;
            var name = V(row, "label")!;
            var bank = existing.FirstOrDefault(b => string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase));
            if (bank is null)
            {
                bank = new Bank { Name = name };
                db.Banks.Add(bank);
                existing.Add(bank);
            }
            map[V(row, "id")!] = bank;
            report.Imported++;
        }
        await db.SaveChangesAsync(cancellationToken);

        var accounts = Add(tables, "compte_bancaires");
        var known = await db.BankAccounts.Where(a => a.DeletedAt == null).ToListAsync(cancellationToken);
        foreach (var row in source.Rows("compte_bancaires"))
        {
            accounts.Read++;
            var id = V(row, "id")!;
            if (!map.TryGetValue(V(row, "banque_id") ?? "", out var bank))
            {
                accounts.Ignore("banque inconnue", id);
                continue;
            }
            var label = V(row, "label")!;
            var account = known.FirstOrDefault(a => string.Equals(a.Label, label, StringComparison.OrdinalIgnoreCase));
            if (account is null)
            {
                account = new BankAccount { Label = label };
                db.BankAccounts.Add(account);
                known.Add(account);
            }
            account.BankId = bank.Id;
            account.Rib = Rib(V(row, "rib"), accounts, $"compte {id} {label}") ?? account.Rib;
            bankAccounts[id] = account;
            accounts.Imported++;
        }
        await db.SaveChangesAsync(cancellationToken);
        return map;
    }

    /// <summary>`general_configs` + `app_configs` (valeurs en MAD, réglages en centimes côté .NET).</summary>
    private async Task SettingsAsync(Source source, List<TableReport> tables, CancellationToken cancellationToken)
    {
        var report = Add(tables, "app_configs");
        var keys = new Dictionary<string, (string Key, bool Cents)>
        {
            ["1"] = ("pricing.failureFee.sameCity", true),
            ["2"] = ("pricing.failureFee.crossCity", true),
            ["3"] = ("pricing.pickupFee", true),
            ["4"] = ("pricing.freePickupThreshold", false),
        };
        foreach (var row in source.Rows("app_configs"))
        {
            report.Read++;
            var config = V(row, "config_id") ?? "";
            if (V(row, "agence_id") is not null)
            {
                report.Ignore("réglage d'agence (aucun équivalent)", config);
                continue;
            }
            if (!keys.TryGetValue(config, out var target))
            {
                report.Ignore(config == "5" ? "« afficher les colis à la préparation d'un circuit » : aucun réglage .NET" : "« e-mail en copie » : aucun réglage .NET", config);
                continue;
            }
            var value = Mad(V(row, "value")) ?? 0m;
            var json = target.Cents ? ((long)Math.Round(value * 100m)).ToString(CultureInfo.InvariantCulture) : ((long)value).ToString(CultureInfo.InvariantCulture);
            var id = $"{DatabaseSeeder.CompanyId}:company:{target.Key}";
            var setting = await db.Settings.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
            if (setting is null) db.Settings.Add(new Setting { Id = id, Scope = SettingScope.COMPANY, Key = target.Key, Value = json });
            else setting.Value = json;
            report.Imported++;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Les statuts ne s'importent pas : le catalogue .NET garde les identifiants Laravel (1 à 38, sauf 15, 30, 36).</summary>
    private static void StatusesReport(Source source, List<TableReport> tables)
    {
        var report = Add(tables, "colis_statuts");
        var known = Domain.Parcels.StatusCatalog.All.Select(s => s.Id.ToString(CultureInfo.InvariantCulture)).ToHashSet();
        foreach (var row in source.Rows("colis_statuts"))
        {
            report.Read++;
            var id = V(row, "id")!;
            if (known.Contains(id)) report.Imported++;
            else report.Ignore("aucun statut .NET (identifiant absent du catalogue)", $"{id} {V(row, "label")}");
        }
        report.Notes.Add("« importés » = même identifiant dans le catalogue .NET, rien n'est écrit.");
    }

    private sealed record LegacyUser(string LegacyId, User User, bool Created);

    private async Task<Dictionary<string, LegacyUser>> UsersAsync(Source source, Dictionary<string, Agency> agencies, List<TableReport> tables, CancellationToken cancellationToken)
    {
        var report = Add(tables, "users");
        var rows = source.Rows("users");
        var modules = source.Rows("user_modules").GroupBy(m => V(m, "user_id")!).ToDictionary(g => g.Key, g => g.Select(m => V(m, "module_id")).Distinct().Count());
        // E-mails en double : le compte actif (non supprimé, activé), puis le plus récent.
        var winners = rows.GroupBy(r => (V(r, "email") ?? string.Empty).ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => !Deleted(r)).ThenByDescending(r => V(r, "enabled") == "1").ThenByDescending(r => At(V(r, "updated_at"))).First());
        var existing = await db.Users.Include(u => u.Roles).Include(u => u.Agencies).Where(u => u.Email != null).ToListAsync(cancellationToken);
        // Un vendeur (rôle 2) ou un livreur (rôle 4) n'existe que par sa fiche active : sans elle, pas de compte orphelin.
        var merchants = source.Rows("clients").Where(c => !Deleted(c)).Select(c => V(c, "user_id")).ToHashSet();
        var couriers = source.Rows("livreurs").Where(c => !Deleted(c)).Select(c => V(c, "user_id")).ToHashSet();
        var map = new Dictionary<string, LegacyUser>();
        foreach (var row in rows)
        {
            report.Read++;
            var id = V(row, "id")!;
            var email = (V(row, "email") ?? string.Empty).ToLowerInvariant();
            var label = $"{id} {email}";
            if (V(row, "partner_slug") is { } partner)
            {
                report.Ignore("compte partenaire API (listé, non importé)", $"{id} {partner}");
                continue;
            }
            if (Deleted(row))
            {
                report.Ignore("compte supprimé dans Laravel", label);
                continue;
            }
            if (!ReferenceEquals(winners[email], row))
            {
                report.Ignore("e-mail en double (le compte actif est gardé)", label);
                continue;
            }
            var role = V(row, "role_id") switch
            {
                "1" => modules.GetValueOrDefault(id) is 0 or AllModules ? RoleName.SUPER_ADMIN : RoleName.STAFF,
                "2" => RoleName.MERCHANT,
                "4" => RoleName.COURIER,
                _ => (RoleName?)null,
            };
            if (role is null)
            {
                report.Ignore($"rôle Laravel {V(row, "role_id")} inconnu", label);
                continue;
            }
            if ((role == RoleName.MERCHANT && !merchants.Contains(id)) || (role == RoleName.COURIER && !couriers.Contains(id)))
            {
                report.Ignore(role == RoleName.MERCHANT ? "vendeur sans fiche client active" : "livreur sans fiche livreur active", label);
                continue;
            }
            var user = existing.FirstOrDefault(u => u.Email == email);
            var created = user is null;
            if (user is null)
            {
                user = new User
                {
                    Email = email,
                    // L'empreinte bcrypt `$2y$` telle quelle : vérifiée puis refaite en argon2id à la première connexion.
                    PasswordHash = V(row, "password") ?? string.Empty,
                    Status = UserStatus.ACTIVE,
                    TokensValidFrom = DateTime.UtcNow.AddMinutes(-1),
                    CreatedAt = At(V(row, "created_at")) ?? DateTime.UtcNow,
                };
                db.Users.Add(user);
                existing.Add(user);
                user.FirstName = V(row, "prenom") ?? string.Empty;
                user.LastName = V(row, "nom") ?? string.Empty;
                user.Phone = V(row, "tel");
                user.NationalId = V(row, "cni");
                user.JobTitle = V(row, "fonction");
                user.IsActive = V(row, "enabled") == "1";
            }
            else
            {
                // Compte déjà présent (wimo@gmail.com en production) : rapproché par e-mail, ni mot de passe ni profil écrasés.
                report.Notes.Add($"{label} : compte .NET existant, rapproché par e-mail (mot de passe et profil inchangés).");
            }
            if (user.Roles.All(r => r.Role != role)) user.Roles.Add(new UserRole { UserId = user.Id, Role = role.Value });
            var agencyIds = new[] { V(row, "agence_id") }.Concat((V(row, "agences") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where(a => a is not null && agencies.ContainsKey(a)).Select(a => agencies[a!].Id).Distinct().ToList();
            if (role == RoleName.STAFF && created)
            {
                user.AgencyId = agencyIds.FirstOrDefault();
                foreach (var agencyId in agencyIds.Where(a => user.Agencies.All(l => l.AgencyId != a))) user.Agencies.Add(new UserAgency { UserId = user.Id, AgencyId = agencyId });
            }
            else if (role == RoleName.COURIER && created)
            {
                user.AgencyId = agencyIds.FirstOrDefault();
            }
            map[id] = new LegacyUser(id, user, created);
            if (role is RoleName.SUPER_ADMIN or RoleName.STAFF) report.Notes.Add($"{label} → {role} ({modules.GetValueOrDefault(id)} droit(s) Laravel).");
            report.Imported++;
        }
        await db.SaveChangesAsync(cancellationToken);
        return map;
    }

    private async Task<Dictionary<string, Client>> ClientsAsync(Source source, Dictionary<string, LegacyUser> users, Dictionary<string, City> cities, Dictionary<string, Agency> agencies,
        List<TableReport> tables, CancellationToken cancellationToken)
    {
        var report = Add(tables, "clients");
        var userRows = source.Rows("users").ToDictionary(r => V(r, "id")!);
        var banks = source.Rows("banques").ToDictionary(r => V(r, "id")!, r => V(r, "label"));
        var existing = await db.Clients.Where(c => c.UserId != null).ToListAsync(cancellationToken);
        var map = new Dictionary<string, Client>();
        foreach (var row in source.Rows("clients"))
        {
            report.Read++;
            var id = V(row, "id")!;
            var label = V(row, "label");
            if (Deleted(row))
            {
                report.Ignore("client supprimé dans Laravel", $"{id} {label}");
                continue;
            }
            if (!users.TryGetValue(V(row, "user_id") ?? "", out var legacy) || legacy.User.Roles.All(r => r.Role != RoleName.MERCHANT))
            {
                report.Ignore("compte vendeur non importé (supprimé ou en double)", $"{id} {label}");
                continue;
            }
            var account = userRows[legacy.LegacyId];
            var agency = agencies.GetValueOrDefault(V(row, "agence_id") ?? "");
            var city = cities.GetValueOrDefault(V(account, "ville_id") ?? "") ?? (agency is null ? null : cities.Values.FirstOrDefault(c => c.Id == agency.CityId));
            if (city is null)
            {
                report.Ignore("aucune ville (ni du compte ni de l'agence)", $"{id} {label}");
                continue;
            }
            var client = existing.FirstOrDefault(c => c.UserId == legacy.User.Id);
            if (client is null)
            {
                client = new Client { UserId = legacy.User.Id, CreatedAt = At(V(row, "created_at")) ?? DateTime.UtcNow };
                db.Clients.Add(client);
                existing.Add(client);
            }
            client.Name = label ?? $"{legacy.User.FirstName} {legacy.User.LastName}".Trim();
            client.Type = V(row, "type") == "particulier" ? ClientType.INDIVIDUAL : ClientType.MERCHANT;
            client.TaxId = V(row, "ice") ?? client.TaxId;
            client.Phone = legacy.User.Phone ?? string.Empty;
            client.Email = legacy.User.Email!;
            client.CityId = city.Id;
            client.AgencyId = agency?.Id ?? client.AgencyId;
            client.IsActive = V(row, "enabled") == "1";
            // Laravel ne bloque pas un vendeur sans date de validation : il est validé à sa création.
            client.ValidatedAt ??= At(V(row, "validation_date")) ?? client.CreatedAt;
            client.DeletedAt = null;
            if (V(account, "rib") is { } rib)
            {
                report.Notes.Add($"{id} {client.Name} : RIB Laravel {rib} ({banks.GetValueOrDefault(V(account, "banque_id") ?? "") ?? "banque inconnue"}) — aucune colonne .NET, non repris.");
            }
            else if (V(account, "banque_id") is { } bank && banks.ContainsKey(bank))
            {
                report.Notes.Add($"{id} {client.Name} : banque Laravel {banks.GetValueOrDefault(bank)} sans RIB — non reprise.");
            }
            if (V(row, "label") is null) report.Notes.Add($"{id} : sans libellé Laravel, nommé « {client.Name} ».");
            map[id] = client;
            report.Imported++;
        }
        var orphans = source.Rows("users").Where(u => V(u, "role_id") == "2" && !Deleted(u) && source.Rows("clients").All(c => V(c, "user_id") != V(u, "id"))).Select(u => V(u, "id")).ToList();
        foreach (var orphan in orphans) report.Notes.Add($"user {orphan} : rôle vendeur sans fiche client — ignoré.");
        await db.SaveChangesAsync(cancellationToken);
        return map;
    }

    /// <summary>
    /// `client_remises`. Laravel n'applique que `enabled = true` (`ClientRemise::where('enabled', true)`) : NULL et 0 sont
    /// inactives. `meme_ville` est un PRIX (même ville), `frais_livraison` le prix sinon, `frais_ramassage` le ramassage.
    /// </summary>
    private async Task DiscountsAsync(Source source, Dictionary<string, Client> clients, Dictionary<string, Zone> zones, List<TableReport> tables, CancellationToken cancellationToken)
    {
        var report = Add(tables, "client_remises");
        var clientIds = clients.Values.Select(c => c.Id).ToList();
        var existing = await db.ClientDiscounts.Where(d => clientIds.Contains(d.ClientId)).ToListAsync(cancellationToken);
        var seen = new HashSet<(string, string)>();
        foreach (var row in source.Rows("client_remises").OrderBy(r => int.Parse(V(r, "id")!, CultureInfo.InvariantCulture)))
        {
            report.Read++;
            var id = V(row, "id")!;
            if (!clients.TryGetValue(V(row, "client_id") ?? "", out var client))
            {
                report.Ignore("client non importé", id);
                continue;
            }
            if (!zones.TryGetValue(V(row, "zone_id") ?? "", out var zone))
            {
                report.Ignore("zone inconnue", id);
                continue;
            }
            if (!seen.Add((client.Id, zone.Id)))
            {
                report.Ignore("seconde remise du même client sur la même zone (la première, par id, est gardée)", id);
                continue;
            }
            var discount = existing.FirstOrDefault(d => d.ClientId == client.Id && d.ZoneIds.Count == 1 && d.ZoneIds[0] == zone.Id);
            if (discount is null)
            {
                discount = new ClientDiscount { ClientId = client.Id, ZoneIds = [zone.Id], CreatedAt = At(V(row, "created_at")) ?? DateTime.UtcNow };
                db.ClientDiscounts.Add(discount);
                existing.Add(discount);
            }
            discount.FlatRate = Mad(V(row, "frais_livraison"));
            discount.SameCityRate = Mad(V(row, "meme_ville"));
            discount.PickupFee = Mad(V(row, "frais_ramassage"));
            discount.IsActive = V(row, "enabled") == "1";
            report.Imported++;
        }
        report.Notes.Add("règle Laravel : seule une remise `enabled = 1` s'applique (NULL ou 0 = inactive, reprise inactive).");
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task PickupAddressesAsync(Source source, Dictionary<string, Client> clients, Dictionary<string, City> cities, Dictionary<string, District> districts,
        Dictionary<string, LegacyUser> users, List<TableReport> tables, CancellationToken cancellationToken)
    {
        var report = Add(tables, "client_ramassage_adresses");
        var clientIds = clients.Values.Select(c => c.Id).ToList();
        var existing = await db.ClientPickupAddresses.Where(a => clientIds.Contains(a.ClientId) && a.DeletedAt == null).ToListAsync(cancellationToken);
        foreach (var row in source.Rows("client_ramassage_adresses").OrderBy(r => int.Parse(V(r, "id")!, CultureInfo.InvariantCulture)))
        {
            report.Read++;
            var id = V(row, "id")!;
            if (!clients.TryGetValue(V(row, "client_id") ?? "", out var client))
            {
                report.Ignore("client non importé", id);
                continue;
            }
            if (!cities.TryGetValue(V(row, "ville_id") ?? "", out var city))
            {
                report.Ignore("ville inconnue", id);
                continue;
            }
            var text = V(row, "adresse") ?? string.Empty;
            if (text.Length == 0)
            {
                report.Ignore("adresse vide", id);
                continue;
            }
            var address = existing.FirstOrDefault(a => a.ClientId == client.Id && a.CityId == city.Id && string.Equals(a.Address, text, StringComparison.OrdinalIgnoreCase));
            if (address is not null && db.Entry(address).State == EntityState.Added)
            {
                report.Ignore("adresse identique déjà reprise pour ce client (même ville, même texte)", id);
                continue;
            }
            if (address is null)
            {
                var rank = existing.Count(a => a.ClientId == client.Id) + 1;
                address = new ClientPickupAddress { ClientId = client.Id, Label = $"Adresse de ramassage {rank}", Address = text, CityId = city.Id, IsDefault = rank == 1, Phone = client.Phone };
                db.ClientPickupAddresses.Add(address);
                existing.Add(address);
            }
            if (V(row, "ramassage_type") == "App\\Quartier" && V(row, "ramassage_id") is { } quarter && districts.TryGetValue(quarter, out var district)) address.DistrictId = district.Id;
            report.Imported++;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Dictionary<string, Courier>> CouriersAsync(Source source, Dictionary<string, LegacyUser> users, Dictionary<string, City> cities, Dictionary<string, Agency> agencies,
        Dictionary<string, Bank> banks, List<TableReport> tables, CancellationToken cancellationToken)
    {
        var report = Add(tables, "livreurs");
        var userRows = source.Rows("users").ToDictionary(r => V(r, "id")!);
        var existing = await db.Couriers.Where(c => c.UserId != null).ToListAsync(cancellationToken);
        var map = new Dictionary<string, Courier>();
        foreach (var row in source.Rows("livreurs"))
        {
            report.Read++;
            var id = V(row, "id")!;
            if (Deleted(row))
            {
                report.Ignore("livreur supprimé dans Laravel", id);
                continue;
            }
            if (!users.TryGetValue(V(row, "user_id") ?? "", out var legacy) || legacy.User.Roles.All(r => r.Role != RoleName.COURIER))
            {
                report.Ignore("compte livreur non importé (supprimé ou en double)", id);
                continue;
            }
            var account = userRows[legacy.LegacyId];
            var agency = agencies.GetValueOrDefault(V(account, "agence_id") ?? "");
            var city = cities.GetValueOrDefault(V(row, "ville_id") ?? "") ?? cities.GetValueOrDefault(V(account, "ville_id") ?? "")
                       ?? (agency is null ? null : cities.Values.FirstOrDefault(c => c.Id == agency.CityId));
            if (city is null)
            {
                report.Ignore("aucune ville", id);
                continue;
            }
            var courier = existing.FirstOrDefault(c => c.UserId == legacy.User.Id);
            if (courier is null)
            {
                courier = new Courier { UserId = legacy.User.Id, CreatedAt = At(V(row, "created_at")) ?? DateTime.UtcNow };
                db.Couriers.Add(courier);
                existing.Add(courier);
            }
            courier.FirstName = legacy.User.FirstName;
            courier.LastName = legacy.User.LastName;
            courier.Phone = legacy.User.Phone ?? string.Empty;
            courier.CityId = city.Id;
            courier.AgencyId = agency?.Id ?? courier.AgencyId;
            courier.NationalId = V(account, "cni") ?? courier.NationalId;
            courier.VehicleType = V(row, "vehicule_type_id") switch { "2" => VehicleType.CAR, "3" => VehicleType.VAN, _ => VehicleType.MOTORBIKE };
            courier.IsActive = V(row, "enabled") == "1";
            courier.BankName = banks.GetValueOrDefault(V(account, "banque_id") ?? "")?.Name ?? courier.BankName;
            courier.Rib = Rib(V(account, "rib"), report, $"livreur {id}") ?? courier.Rib;
            courier.ValidatedAt ??= At(V(row, "validation_date")) ?? courier.CreatedAt;
            courier.DeletedAt = null;
            map[id] = courier;
            report.Imported++;
        }
        await db.SaveChangesAsync(cancellationToken);
        return map;
    }

    private async Task CourierCitiesAsync(Source source, Dictionary<string, Courier> couriers, Dictionary<string, City> cities, List<TableReport> tables, CancellationToken cancellationToken)
    {
        var report = Add(tables, "livreur_ville");
        var courierIds = couriers.Values.Select(c => c.Id).ToList();
        var existing = (await db.CourierCities.Where(c => courierIds.Contains(c.CourierId)).ToListAsync(cancellationToken)).ToDictionary(c => (c.CourierId, c.CityId));
        // `grilleLivreur` de Laravel : la PREMIÈRE ligne d'une paire livreur + ville gagne, mais une ligne à
        // `frais_livraison` NULL ne compte pas (ville NON tarifée, pas un tarif à zéro).
        var taken = new HashSet<(string, string)>();
        foreach (var row in source.Rows("livreur_ville").OrderBy(r => int.Parse(V(r, "id")!, CultureInfo.InvariantCulture)))
        {
            report.Read++;
            var id = V(row, "id")!;
            if (!couriers.TryGetValue(V(row, "livreur_id") ?? "", out var courier))
            {
                report.Ignore("livreur non importé", id);
                continue;
            }
            if (!cities.TryGetValue(V(row, "ville_id") ?? "", out var city))
            {
                report.Ignore("ville supprimée", id);
                continue;
            }
            if (Mad(V(row, "frais_livraison")) is not { } fee)
            {
                report.Ignore("frais_livraison NULL : ville non tarifée pour ce livreur (Laravel la signale, ne la compte pas à 0)", id);
                continue;
            }
            if (!taken.Add((courier.Id, city.Id)))
            {
                report.Ignore("doublon livreur + ville (Laravel garde la première ligne tarifée)", id);
                continue;
            }
            if (!existing.TryGetValue((courier.Id, city.Id), out var line))
            {
                line = new CourierCity { CourierId = courier.Id, CityId = city.Id };
                db.CourierCities.Add(line);
                existing[(courier.Id, city.Id)] = line;
            }
            line.DeliveryFee = fee;
            line.PickupFee = Mad(V(row, "frais_ramassage")) ?? 0m;
            report.Imported++;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static TableReport Add(List<TableReport> tables, string name)
    {
        var report = new TableReport(name);
        tables.Add(report);
        return report;
    }
}
