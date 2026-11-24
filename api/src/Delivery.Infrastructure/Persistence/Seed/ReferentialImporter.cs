using System.Globalization;
using System.Text;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Infrastructure.Persistence.Seed;

/// <summary>
/// Import idempotent du référentiel depuis un dossier de CSV (UTF-8, séparateur `,`,
/// en-tête obligatoire). Chaque fichier est facultatif ; les clés naturelles
/// (nom de zone, code de ville, ville + quartier, nom d'agence, paire de villes)
/// font qu'un rejeu met à jour au lieu de dupliquer.
/// <list type="bullet">
/// <item>`zones.csv` : name, baseRate, sameCityRate, deliveryDays</item>
/// <item>`cities.csv` : code, name, nameAr, zone, deliveryDelay, isActive</item>
/// <item>`districts.csv` : cityCode, name</item>
/// <item>`agencies.csv` : name, cityCode, phone, email, address, coveredCityCodes (séparés par `;`)</item>
/// <item>`pricing-rules.csv` : originCode, destinationCode, rateDistrict, rateAgency, rateRelayPoint, isEnabled</item>
/// </list>
/// Montants en MAD. Rien n'est jamais supprimé.
/// </summary>
public sealed class ReferentialImporter(AppDbContext db, ILogger<ReferentialImporter> logger)
{
    public sealed record Report(int Zones, int Cities, int Districts, int Agencies, int Rules, IReadOnlyList<string> Warnings);

    public async Task<Report> ImportAsync(string folder, CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        var zones = await ImportZonesAsync(Path.Combine(folder, "zones.csv"), warnings, cancellationToken);
        var cities = await ImportCitiesAsync(Path.Combine(folder, "cities.csv"), warnings, cancellationToken);
        var districts = await ImportDistrictsAsync(Path.Combine(folder, "districts.csv"), warnings, cancellationToken);
        var agencies = await ImportAgenciesAsync(Path.Combine(folder, "agencies.csv"), warnings, cancellationToken);
        var rules = await ImportRulesAsync(Path.Combine(folder, "pricing-rules.csv"), warnings, cancellationToken);
        foreach (var warning in warnings) logger.LogWarning("import-referential : {Warning}", warning);
        return new Report(zones, cities, districts, agencies, rules, warnings);
    }

    private async Task<int> ImportZonesAsync(string file, List<string> warnings, CancellationToken cancellationToken)
    {
        var rows = Csv.Read(file);
        if (rows is null) return 0;
        var existing = await db.Zones.Where(z => z.DeletedAt == null).ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            var name = row.Get("name");
            if (name.Length == 0) continue;
            var zone = existing.FirstOrDefault(z => string.Equals(z.Name, name, StringComparison.OrdinalIgnoreCase));
            if (zone is null)
            {
                zone = new Zone { Name = name };
                db.Zones.Add(zone);
                existing.Add(zone);
            }
            zone.BaseRate = row.Money("baseRate", warnings) ?? zone.BaseRate;
            zone.SameCityRate = row.Money("sameCityRate", warnings) ?? zone.SameCityRate;
            if (int.TryParse(row.Get("deliveryDays"), CultureInfo.InvariantCulture, out var days)) zone.DeliveryDays = days;
        }
        await db.SaveChangesAsync(cancellationToken);
        return rows.Count;
    }

    private async Task<int> ImportCitiesAsync(string file, List<string> warnings, CancellationToken cancellationToken)
    {
        var rows = Csv.Read(file);
        if (rows is null) return 0;
        var zones = await db.Zones.Where(z => z.DeletedAt == null).ToListAsync(cancellationToken);
        var cities = await db.Cities.Include(c => c.ZoneLinks).ToListAsync(cancellationToken);
        var count = 0;
        foreach (var row in rows)
        {
            var code = row.Get("code").ToUpperInvariant();
            var name = row.Get("name");
            if (code.Length is < 2 or > 3 || !code.All(char.IsAsciiLetterUpper))
            {
                warnings.Add($"cities.csv ligne {row.Line} : code « {code} » invalide (2 ou 3 lettres).");
                continue;
            }
            var zone = zones.FirstOrDefault(z => string.Equals(z.Name, row.Get("zone"), StringComparison.OrdinalIgnoreCase));
            if (zone is null)
            {
                warnings.Add($"cities.csv ligne {row.Line} : zone « {row.Get("zone")} » inconnue, ville {code} ignorée.");
                continue;
            }
            var city = cities.FirstOrDefault(c => c.Code == code);
            if (city is null)
            {
                city = new City { Code = code };
                db.Cities.Add(city);
                cities.Add(city);
            }
            var (clean, arabic) = CityNames.Normalize(name.Length > 0 ? name : city.Name);
            city.Name = clean;
            city.NameAr = row.Get("nameAr") is { Length: > 0 } ar ? ar : arabic ?? city.NameAr;
            city.ZoneId = zone.Id;
            city.DeliveryDelay = row.Get("deliveryDelay") is { Length: > 0 } delay ? delay : city.DeliveryDelay;
            city.IsActive = row.Bool("isActive") ?? true;
            city.DeletedAt = null;
            if (city.ZoneLinks.All(l => l.ZoneId != zone.Id)) city.ZoneLinks.Add(new ZoneCity { ZoneId = zone.Id, CityId = city.Id });
            count++;
        }
        await db.SaveChangesAsync(cancellationToken);
        return count;
    }

    private async Task<int> ImportDistrictsAsync(string file, List<string> warnings, CancellationToken cancellationToken)
    {
        var rows = Csv.Read(file);
        if (rows is null) return 0;
        var cityByCode = await db.Cities.Where(c => c.DeletedAt == null).ToDictionaryAsync(c => c.Code, c => c.Id, cancellationToken);
        var known = (await db.Districts.Where(d => d.DeletedAt == null).Select(d => new { d.CityId, d.Name }).ToListAsync(cancellationToken))
            .Select(d => (d.CityId, CityNames.DistrictKey(d.Name))).ToHashSet();
        var count = 0;
        foreach (var row in rows)
        {
            if (!cityByCode.TryGetValue(row.Get("cityCode").ToUpperInvariant(), out var cityId))
            {
                warnings.Add($"districts.csv ligne {row.Line} : ville « {row.Get("cityCode")} » inconnue.");
                continue;
            }
            var name = row.Get("name");
            if (name.Length < 2 || !known.Add((cityId, CityNames.DistrictKey(name)))) continue;
            db.Districts.Add(new District { CityId = cityId, Name = name });
            count++;
        }
        await db.SaveChangesAsync(cancellationToken);
        return count;
    }

    private async Task<int> ImportAgenciesAsync(string file, List<string> warnings, CancellationToken cancellationToken)
    {
        var rows = Csv.Read(file);
        if (rows is null) return 0;
        var cityByCode = await db.Cities.Where(c => c.DeletedAt == null).ToDictionaryAsync(c => c.Code, c => c.Id, cancellationToken);
        var agencies = await db.Agencies.Include(a => a.CityLinks).Where(a => a.DeletedAt == null).ToListAsync(cancellationToken);
        var count = 0;
        foreach (var row in rows)
        {
            var name = row.Get("name");
            if (name.Length == 0) continue;
            if (!cityByCode.TryGetValue(row.Get("cityCode").ToUpperInvariant(), out var cityId))
            {
                warnings.Add($"agencies.csv ligne {row.Line} : ville « {row.Get("cityCode")} » inconnue, agence ignorée.");
                continue;
            }
            var agency = agencies.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
            if (agency is null)
            {
                agency = new Agency { Name = name };
                db.Agencies.Add(agency);
                agencies.Add(agency);
            }
            agency.CityId = cityId;
            agency.Phone = row.Get("phone") is { Length: > 0 } phone ? phone : agency.Phone;
            agency.Email = row.Get("email") is { Length: > 0 } email ? email : agency.Email;
            agency.Address = row.Get("address") is { Length: > 0 } address ? address : agency.Address;
            foreach (var code in row.Get("coveredCityCodes").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!cityByCode.TryGetValue(code.ToUpperInvariant(), out var covered)) warnings.Add($"agencies.csv ligne {row.Line} : ville couverte « {code} » inconnue.");
                else if (covered != cityId && agency.CityLinks.All(l => l.CityId != covered)) agency.CityLinks.Add(new AgencyCity { AgencyId = agency.Id, CityId = covered });
            }
            count++;
        }
        await db.SaveChangesAsync(cancellationToken);
        return count;
    }

    private async Task<int> ImportRulesAsync(string file, List<string> warnings, CancellationToken cancellationToken)
    {
        var rows = Csv.Read(file);
        if (rows is null) return 0;
        var cityByCode = await db.Cities.Where(c => c.DeletedAt == null).ToDictionaryAsync(c => c.Code, c => c.Id, cancellationToken);
        var rules = await db.PricingRules.ToListAsync(cancellationToken);
        var count = 0;
        foreach (var row in rows)
        {
            if (!cityByCode.TryGetValue(row.Get("originCode").ToUpperInvariant(), out var origin) || !cityByCode.TryGetValue(row.Get("destinationCode").ToUpperInvariant(), out var destination))
            {
                warnings.Add($"pricing-rules.csv ligne {row.Line} : ville inconnue ({row.Get("originCode")} → {row.Get("destinationCode")}).");
                continue;
            }
            var district = row.Money("rateDistrict", warnings);
            if (district is null) continue;
            var agencyRate = row.Money("rateAgency", warnings) ?? district.Value;
            var relay = row.Money("rateRelayPoint", warnings) ?? agencyRate;
            var rule = rules.FirstOrDefault(r => r.OriginCityId == origin && r.DestinationCityId == destination);
            if (rule is null)
            {
                rule = new PricingRule { OriginCityId = origin, DestinationCityId = destination };
                db.PricingRules.Add(rule);
                rules.Add(rule);
            }
            rule.RateDistrict = district.Value;
            rule.RateAgency = agencyRate;
            rule.RateRelayPoint = relay;
            rule.IsEnabled = row.Bool("isEnabled") ?? true;
            rule.DeletedAt = null;
            count++;
        }
        await db.SaveChangesAsync(cancellationToken);
        return count;
    }

    /// <summary>Lecteur CSV RFC 4180 minimal (guillemets doublés, retours à la ligne cités).</summary>
    internal static class Csv
    {
        public sealed class Row(int line, Dictionary<string, string> cells)
        {
            public int Line { get; } = line;

            public string Get(string column) => cells.TryGetValue(column, out var value) ? value.Trim() : string.Empty;

            public bool? Bool(string column) => Get(column).ToLowerInvariant() switch
            {
                "" => null,
                "1" or "true" or "oui" or "yes" => true,
                _ => false,
            };

            public decimal? Money(string column, List<string> warnings)
            {
                var text = Get(column).Replace(',', '.');
                if (text.Length == 0) return null;
                if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value >= 0 && value <= 1_000_000) return Math.Round(value, 2);
                warnings.Add($"ligne {Line} : montant « {Get(column)} » illisible ({column}).");
                return null;
            }
        }

        public static List<Row>? Read(string file)
        {
            if (!File.Exists(file)) return null;
            var records = Parse(File.ReadAllText(file, Encoding.UTF8).TrimStart('﻿'));
            if (records.Count == 0) return [];
            var header = records[0].Select(h => h.Trim()).ToList();
            return records.Skip(1).Select((cells, i) => new Row(i + 2, header.Select((h, c) => (h, c < cells.Count ? cells[c] : string.Empty)).ToDictionary(x => x.h, x => x.Item2)))
                .Where(r => header.Any(h => r.Get(h).Length > 0))
                .ToList();
        }

        private static List<List<string>> Parse(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            var quoted = false;
            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                if (quoted)
                {
                    if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else if (ch == '"') quoted = false;
                    else cell.Append(ch);
                    continue;
                }
                switch (ch)
                {
                    case '"':
                        quoted = true;
                        break;
                    case ',':
                        row.Add(cell.ToString());
                        cell.Clear();
                        break;
                    case '\r':
                        break;
                    case '\n':
                        row.Add(cell.ToString());
                        cell.Clear();
                        rows.Add(row);
                        row = [];
                        break;
                    default:
                        cell.Append(ch);
                        break;
                }
            }
            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                rows.Add(row);
            }
            return rows;
        }
    }
}
