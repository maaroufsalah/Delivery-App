using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using Delivery.Domain.Parcels;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Infrastructure.Persistence.Seed;

/// <summary>
/// Reprise de l'opérationnel et de l'argent de Wimo Laravel (`docs/migration/laravel-mapping.md`) : colis (code
/// d'origine), historique, bons, circuits, factures, règlements, relevés et caisses des livreurs. Les montants sont ceux
/// qu'affiche Laravel (ses formules appliquées une fois, à l'import), stockés, jamais recalculés ensuite par .NET.
/// Un document clos dans Laravel est marqué `ImportedAt` (verrouillé) ; un document encore ouvert (brouillon, caisse non
/// reçue, circuit en cours) reste vivant dans .NET. Chaque ligne reprise a sa ligne `legacy_mappings` et son empreinte :
/// une relance ne touche que ce qui a changé côté Laravel.
/// </summary>
public sealed partial class LaravelImporter
{
    public const string ImportNote = "Reprise Wimo Laravel";

    /// <summary>Statuts Laravel sans équivalent .NET, tranchés au plus proche.</summary>
    public static readonly IReadOnlyDictionary<int, int> NearestStatus = new Dictionary<int, int>
    {
        [15] = 16, // En agence destination → En attente de livraison
        [30] = 7,  // Au guichet Amana → Expédié
        [36] = 33, // 3eme appel Pas de réponse + SMS → 3eme appel Pas de réponse
    };

    private const int DeliveredStatus = 4;
    private const int PickedUpByCourierStatus = 18;

    private Dictionary<(string Entity, string LegacyId), LegacyMapping> mappings = [];

    // ------------------------------------------------------------------ correspondances

    private static string Hash(params object?[] values)
    {
        var text = string.Join('\u001f', values.Select(v => v switch
        {
            null => "∅",
            decimal d => d.ToString("0.00##", CultureInfo.InvariantCulture),
            DateTime t => t.ToString("O", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => v.ToString(),
        }));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..32];
    }

    private static string RowHash(Dictionary<string, string?> row) => Hash(row.OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => (object?)$"{c.Key}={c.Value}").ToArray());

    private string? NewId(string entity, string? legacyId) => legacyId is not null && mappings.TryGetValue((entity, legacyId), out var m) ? m.NewId : null;

    private enum Outcome { Created, Updated, Unchanged }

    /// <summary>Enregistre la correspondance (création ou empreinte nouvelle) ; dit si la ligne est neuve, changée ou identique.</summary>
    private Outcome Track(string entity, string legacyId, string newId, string hash)
    {
        var now = DateTime.UtcNow;
        if (mappings.TryGetValue((entity, legacyId), out var known))
        {
            if (known.Hash == hash && known.NewId == newId) return Outcome.Unchanged;
            var updated = new LegacyMapping { Entity = entity, LegacyId = legacyId, NewId = newId, Hash = hash, ImportedAt = known.ImportedAt, UpdatedAt = now };
            db.LegacyMappings.Update(updated);
            mappings[(entity, legacyId)] = updated;
            return Outcome.Updated;
        }
        var created = new LegacyMapping { Entity = entity, LegacyId = legacyId, NewId = newId, Hash = hash, ImportedAt = now, UpdatedAt = now };
        db.LegacyMappings.Add(created);
        mappings[(entity, legacyId)] = created;
        return Outcome.Created;
    }

    private static void Count(TableReport report, Outcome outcome)
    {
        switch (outcome)
        {
            case Outcome.Created: report.Imported++; break;
            case Outcome.Updated: report.Updated++; break;
            default: report.Unchanged++; break;
        }
    }

    private async Task MapReferentialAsync(Source source, Dictionary<string, Zone> zones, Dictionary<string, City> cities, Dictionary<string, District> districts,
        Dictionary<string, Agency> agencies, Dictionary<string, Bank> banks, Dictionary<string, LegacyUser> users, Dictionary<string, Client> clients,
        Dictionary<string, Courier> couriers, List<TableReport> tables, CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken);
        mappings = await db.LegacyMappings.AsNoTracking().ToDictionaryAsync(m => (m.Entity, m.LegacyId), cancellationToken);
        // Ces tables se rapprochent par clé naturelle ; leur ligne `legacy_mappings` dit si la ligne est neuve, changée ou identique.
        void Map<T>(string entity, string table, Dictionary<string, T> map, Func<T, string> id)
        {
            var rows = source.Rows(table).Where(r => V(r, "id") is not null).ToDictionary(r => V(r, "id")!);
            var report = tables.FirstOrDefault(t => t.Table == table);
            if (report is not null) report.Imported = 0;
            foreach (var (legacyId, target) in map)
            {
                if (!rows.TryGetValue(legacyId, out var row)) continue;
                var outcome = Track(entity, legacyId, id(target), RowHash(row));
                if (report is not null) Count(report, outcome);
            }
        }
        Map("zone", "zones", zones, z => z.Id);
        Map("city", "villes", cities, c => c.Id);
        Map("district", "quartiers", districts, d => d.Id);
        Map("agency", "agences", agencies, a => a.Id);
        Map("bank", "banques", banks, b => b.Id);
        Map("bank_account", "compte_bancaires", bankAccounts, a => a.Id);
        Map("user", "users", users.ToDictionary(u => u.Key, u => u.Value.User), u => u.Id);
        Map("client", "clients", clients, c => c.Id);
        Map("courier", "livreurs", couriers, c => c.Id);

        // Délais : `tarifs.delai` au départ de la ville de l'agence (Casablanca), affiché par ville de destination.
        var agencyCity = source.Rows("agences").Where(r => !Deleted(r)).Select(r => V(r, "ville_id")).FirstOrDefault(v => v is not null);
        foreach (var row in source.Rows("tarifs").Where(r => V(r, "depart_id") == agencyCity && V(r, "delai") is not null))
        {
            if (cities.TryGetValue(V(row, "destination_id") ?? "", out var city)) city.DeliveryDelay = V(row, "delai");
        }
    }

    /// <summary>`reclamation_types` : rapprochés par libellé ; Laravel n'en a qu'en lecture (aucune réclamation ouverte).</summary>
    private async Task ClaimTypesAsync(Source source, List<TableReport> tables, CancellationToken cancellationToken)
    {
        if (!source.Has("reclamation_types")) return;
        var report = Add(tables, "reclamation_types");
        var existing = await db.ClaimTypes.ToListAsync(cancellationToken);
        var order = existing.Count == 0 ? 0 : existing.Max(t => t.SortOrder);
        foreach (var row in source.Rows("reclamation_types"))
        {
            report.Read++;
            var label = V(row, "label") ?? string.Empty;
            if (Deleted(row))
            {
                report.Ignore("type supprimé dans Laravel", $"{V(row, "id")} {label}");
                continue;
            }
            var type = existing.FirstOrDefault(t => string.Equals(t.Label, label, StringComparison.OrdinalIgnoreCase));
            if (type is null)
            {
                var code = new string(label.ToLowerInvariant().Normalize(NormalizationForm.FormD).Where(c => char.IsAsciiLetterOrDigit(c) || c == ' ').ToArray()).Trim().Replace(' ', '-');
                type = new ClaimType { Code = $"laravel-{V(row, "id")}-{code}", Label = label, SortOrder = ++order, IsActive = true };
                db.ClaimTypes.Add(type);
                existing.Add(type);
            }
            type.SubjectType = V(row, "type_entite") == "App\\Colis" ? ClaimSubjectType.PARCEL : ClaimSubjectType.GENERAL;
            type.DeletedAt = null;
            report.Imported++;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------------------------ calculs Laravel

    private static decimal D(string? value) => Mad(value) ?? 0m;

    private static bool Is(string? value) => value is not null && value != "0";

    /// <summary>SQL `col != 1` : NULL est exclu (comparaison inconnue).</summary>
    private static bool NotOne(string? value) => value is not null && value != "1";

    /// <summary>Le colis Laravel lu une fois : statut tranché, montants, et ce que ses formules en tirent.</summary>
    private sealed record Colis(Dictionary<string, string?> Row)
    {
        public string Id => V(Row, "id")!;
        public string Code => V(Row, "code")!;
        public int LegacyStatus => int.TryParse(V(Row, "statut_id"), CultureInfo.InvariantCulture, out var s) ? s : 1;
        public int Status => NearestStatus.TryGetValue(LegacyStatus, out var near) ? near : LegacyStatus;
        public decimal Prix => D(V(Row, "prix"));
        public decimal Frais => D(V(Row, "frais"));
        public decimal? FraisEchoue => Mad(V(Row, "frais_echoue"));
        public bool Delivered => LegacyStatus == DeliveredStatus;

        /// <summary>`Colis::montantAEncaisser`.</summary>
        public decimal MontantAEncaisser
        {
            get
            {
                var dejaPaye = Is(V(Row, "deja_paye"));
                if (Is(V(Row, "frais_inclus"))) return Prix != 0 && !dejaPaye ? Prix : 0m;
                if (Frais != 0) return Prix != 0 && !dejaPaye ? Prix + Frais : Frais;
                return 0m;
            }
        }
    }

    /// <summary>Une ligne de facture client, par les relations de `App\Facture` (`FactureResource`).</summary>
    private static (decimal Cod, decimal Fee, decimal Failure, decimal Net) InvoiceLineOf(Colis colis)
    {
        var echoue = colis.FraisEchoue;
        decimal cod = 0m, fee = 0m, failure = 0m, cash = 0m;
        if (colis.Delivered && echoue is null)
        {
            if (NotOne(V(colis.Row, "deja_paye"))) cod += colis.Prix;              // colisLivres
            if (NotOne(V(colis.Row, "frais_inclus"))) cod += colis.Frais;          // colisLivresFraisNonInclu
            fee = colis.Frais;                                                     // colisSumFrais
        }
        if (echoue is not null) failure = echoue.Value;                            // colisSumFraisEchoue
        if (colis.Delivered && Is(V(colis.Row, "frais_deja_paye"))) cash = colis.Frais; // colisSumFraisPayeEspece
        return (cod, fee - cash, failure, cod - fee - failure + cash);
    }

    // ------------------------------------------------------------------ orchestration

    private sealed class Context
    {
        public required Dictionary<string, Colis> Colis { get; init; }
        public required Dictionary<string, Dictionary<string, string?>> Users { get; init; }
        public required Dictionary<string, Dictionary<string, string?>> Livreurs { get; init; }
        public required Dictionary<string, string> Districts { get; init; }
        public required Dictionary<string, Dictionary<int, decimal>> Grids { get; init; }
        public required Dictionary<string, (string Id, DateTime? At, string Courier)> FirstConfirmedRecette { get; init; }
        public required Dictionary<string, Dictionary<string, string?>> Factures { get; init; }
        public required Dictionary<string, string> FactureOfColis { get; init; }
        public required Dictionary<string, string> LastBoxOfColis { get; init; }
        public required string DefaultAgencyId { get; init; }
        public required string? DefaultDeliveryTypeId { get; init; }
        public required Dictionary<string, (string? AgencyId, string CityId)> Clients { get; init; }
        public required Dictionary<string, (string? AgencyId, string CityId)> Couriers { get; init; }
    }

    private string UserName(Context context, string? legacyUserId)
    {
        if (legacyUserId is null || !context.Users.TryGetValue(legacyUserId, out var user)) return ImportNote;
        return $"{V(user, "prenom")} {V(user, "nom")}".Trim() is { Length: > 0 } name ? name : V(user, "email") ?? ImportNote;
    }

    private async Task OperationsAsync(Source source, ImportOptions options, List<TableReport> tables, CancellationToken cancellationToken)
    {
        if (!source.Has("colis")) return;
        mappings = await db.LegacyMappings.AsNoTracking().ToDictionaryAsync(m => (m.Entity, m.LegacyId), cancellationToken);
        var context = await ContextAsync(source, tables, cancellationToken);
        await ParcelsAsync(source, context, options, tables, cancellationToken);
        await EventsAsync(source, context, options, tables, cancellationToken);
        await DeliveryNotesAsync(source, context, options, tables, cancellationToken);
        await RoutesAsync(source, context, options, tables, cancellationToken);
        await InvoicesAsync(source, context, options, tables, cancellationToken);
        await PaymentsAsync(source, context, options, tables, cancellationToken);
        await StatementsAsync(source, context, options, tables, cancellationToken);
        await RemittancesAsync(source, context, options, tables, cancellationToken);
        await ExchangesAsync(context, options, tables, cancellationToken);
        await CountersAsync(context, options, tables, cancellationToken);
    }

    private async Task<Context> ContextAsync(Source source, List<TableReport> tables, CancellationToken cancellationToken)
    {
        var colis = source.Rows("colis").Select(r => new Colis(r)).ToDictionary(c => c.Id);
        var gridRows = source.Rows("livreur_ville").OrderBy(r => int.Parse(V(r, "id")!, CultureInfo.InvariantCulture));
        var grids = new Dictionary<string, Dictionary<int, decimal>>();
        foreach (var row in gridRows)
        {
            // `LivreurFactureService::grilleLivreur` : la première ligne (livreur, ville) dont `frais_livraison` est renseigné.
            if (Mad(V(row, "frais_livraison")) is not { } fee || !int.TryParse(V(row, "ville_id"), CultureInfo.InvariantCulture, out var city)) continue;
            var grid = grids.TryGetValue(V(row, "livreur_id")!, out var g) ? g : grids[V(row, "livreur_id")!] = [];
            grid.TryAdd(city, fee);
        }
        var recettes = source.Rows("recettes").ToDictionary(r => V(r, "id")!);
        var firstConfirmed = new Dictionary<string, (string, DateTime?, string)>();
        foreach (var link in source.Rows("recette_colis")
                     .Select(l => (Link: l, Recette: recettes.GetValueOrDefault(V(l, "recette_id") ?? "")))
                     .Where(x => x.Recette is not null && V(x.Recette, "confirmation") == "1" && V(x.Recette, "confirmation_date") is not null)
                     .OrderBy(x => At(V(x.Recette!, "confirmation_date"))).ThenBy(x => int.Parse(V(x.Recette!, "id")!, CultureInfo.InvariantCulture)))
        {
            firstConfirmed.TryAdd(V(link.Link, "colis_id")!, (V(link.Recette!, "id")!, At(V(link.Recette!, "confirmation_date")), V(link.Recette!, "partenaire_id") ?? ""));
        }
        var factures = source.Rows("factures").ToDictionary(r => V(r, "id")!);
        var factureOfColis = new Dictionary<string, string>();
        foreach (var link in source.Rows("facture_colis").OrderBy(l => int.Parse(V(l, "id")!, CultureInfo.InvariantCulture)))
        {
            if (factures.TryGetValue(V(link, "facture_id") ?? "", out var f) && V(f, "client_id") is not null) factureOfColis.TryAdd(V(link, "colis_id")!, V(f, "id")!);
        }
        var lastBox = source.Rows("boxe_colis").OrderBy(l => int.Parse(V(l, "id")!, CultureInfo.InvariantCulture))
            .GroupBy(l => V(l, "colis_id")!).ToDictionary(g => g.Key, g => V(g.Last(), "boxe_id")!);
        var agencies = await db.Agencies.AsNoTracking().Where(a => a.DeletedAt == null).OrderBy(a => a.CreatedAt).Select(a => a.Id).ToListAsync(cancellationToken);
        var clientIds = mappings.Where(m => m.Key.Entity == "client").Select(m => m.Value.NewId).ToList();
        var courierIds = mappings.Where(m => m.Key.Entity == "courier").Select(m => m.Value.NewId).ToList();
        var clients = await db.Clients.AsNoTracking().Where(c => clientIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => (c.AgencyId, c.CityId), cancellationToken);
        var couriers = await db.Couriers.AsNoTracking().Where(c => courierIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => (c.AgencyId, c.CityId), cancellationToken);
        var deliveryType = await db.DeliveryTypes.AsNoTracking().Where(t => t.DeletedAt == null).OrderByDescending(t => t.IsDefault).Select(t => t.Id).FirstOrDefaultAsync(cancellationToken);
        var statuses = Add(tables, "colis_statuts (tranchés)");
        foreach (var group in colis.Values.Where(c => c.Status != c.LegacyStatus).GroupBy(c => (c.LegacyStatus, c.Status)))
        {
            statuses.Notes.Add($"statut Laravel {group.Key.LegacyStatus} → .NET {group.Key.Status} : {group.Count()} colis.");
        }
        return new Context
        {
            Colis = colis,
            Users = source.Rows("users").ToDictionary(r => V(r, "id")!),
            Livreurs = source.Rows("livreurs").ToDictionary(r => V(r, "id")!),
            Districts = source.Rows("quartiers").ToDictionary(r => V(r, "id")!, r => V(r, "label") ?? string.Empty),
            Grids = grids,
            FirstConfirmedRecette = firstConfirmed,
            Factures = factures,
            FactureOfColis = factureOfColis,
            LastBoxOfColis = lastBox,
            DefaultAgencyId = agencies.FirstOrDefault() ?? throw new InvalidOperationException("Aucune agence .NET : importez le référentiel d'abord."),
            DefaultDeliveryTypeId = deliveryType,
            Clients = clients,
            Couriers = couriers,
        };
    }

    /// <summary>Lots de <paramref name="size"/> lignes, chacun dans <see cref="InBatchAsync"/>.</summary>
    private async Task BatchesAsync<T>(IReadOnlyList<T> items, ImportOptions options, Func<T, Task> work, CancellationToken cancellationToken)
    {
        for (var start = 0; start < items.Count; start += options.BatchSize)
        {
            var batch = items.Skip(start).Take(options.BatchSize).ToList();
            await InBatchAsync(options, async () =>
            {
                foreach (var item in batch) await work(item);
            }, cancellationToken);
        }
    }

    // ------------------------------------------------------------------ colis

    private sealed record ParcelPlan(Colis Colis, string ClientId, string? CourierId, string AgencyId, string OriginCityId, string DestinationCityId, string? DistrictId,
        string? DistrictName, string CurrentCityId, BillingState Billing, FinancialStatus Financial, ParcelKind Kind, bool Reimbursed, DateTime? ReimbursedAt,
        decimal? Commission, string? CommissionCourierId, DateTime? CommissionAt, string? ExternalCode);

    private async Task ParcelsAsync(Source source, Context context, ImportOptions options, List<TableReport> tables, CancellationToken cancellationToken)
    {
        var report = Add(tables, "colis");
        var plans = new List<ParcelPlan>();
        var externals = new HashSet<(string, string)>();
        foreach (var colis in context.Colis.Values.OrderBy(c => V(c.Row, "reference_id") is null ? 0 : 1).ThenBy(c => int.Parse(c.Id, CultureInfo.InvariantCulture)))
        {
            report.Read++;
            var row = colis.Row;
            var label = $"{colis.Id} {colis.Code}";
            if (Deleted(row))
            {
                report.Ignore("colis supprimé dans Laravel", label);
                continue;
            }
            var clientId = NewId("client", V(row, "client_id"));
            if (clientId is null)
            {
                report.Ignore("vendeur non repris (supprimé ou sans fiche)", label);
                continue;
            }
            var origin = NewId("city", V(row, "ramassage_ville_id"));
            var destination = NewId("city", V(row, "livraison_ville_id"));
            if (origin is null || destination is null)
            {
                report.Ignore("ville de ramassage ou de livraison non reprise", label);
                continue;
            }
            var district = V(row, "livraison_type") == "App\\Quartier" ? V(row, "livraison_id") : null;
            var current = V(row, "emplacement_type") == "App\\Ville" ? NewId("city", V(row, "emplacement_id")) : null;
            var invoice = context.FactureOfColis.TryGetValue(colis.Id, out var factureId) ? context.Factures[factureId] : null;
            var billing = V(invoice ?? [], "statut_id") switch
            {
                "3" => BillingState.REIMBURSED,
                "2" => BillingState.INVOICED,
                _ => BillingState.TO_INVOICE,
            };
            var recette = context.FirstConfirmedRecette.TryGetValue(colis.Id, out var r) && colis.Delivered ? r : default;
            var courierOfRecette = recette.Id is null ? null : NewId("courier", recette.Courier);
            var reimbursed = recette.Id is not null && courierOfRecette is not null;
            decimal? commission = null;
            if (reimbursed && int.TryParse(V(row, "livraison_ville_id"), CultureInfo.InvariantCulture, out var city)
                           && context.Grids.TryGetValue(recette.Courier, out var grid) && grid.TryGetValue(city, out var fee)) commission = fee;
            var financial = billing switch
            {
                BillingState.REIMBURSED => FinancialStatus.REMBOURSE,
                BillingState.INVOICED => FinancialStatus.FACTURE,
                _ when reimbursed => FinancialStatus.REMIS_LIVREUR,
                _ when colis.Delivered => FinancialStatus.ENCAISSE,
                _ => FinancialStatus.NON_ENCAISSE,
            };
            var kind = colis.Code.StartsWith(ExchangeCodePrefix, StringComparison.Ordinal) ? ParcelKind.EXCHANGE
                : colis.Code.StartsWith(ReturnCodePrefix, StringComparison.Ordinal) ? ParcelKind.RETURN : ParcelKind.STANDARD;
            var external = V(row, "code_externe");
            if (external is not null && !externals.Add((clientId, external)))
            {
                report.Notes.Add($"{label} : code externe « {external} » déjà porté par un autre colis du vendeur — non repris.");
                external = null;
            }
            var clientInfo = context.Clients.GetValueOrDefault(clientId);
            var agency = NewId("agency", V(row, "sys_agence")) ?? clientInfo.AgencyId ?? context.DefaultAgencyId;
            plans.Add(new ParcelPlan(colis, clientId, NewId("courier", V(row, "livreur_id")), agency, origin, destination, district is null ? null : NewId("district", district),
                district is null ? null : context.Districts.GetValueOrDefault(district), current ?? origin, billing, financial, kind, reimbursed,
                reimbursed ? recette.At : null, commission, commission is null ? null : courierOfRecette, commission is null ? null : recette.At, external));
        }
        await BatchesAsync(plans, options, async plan =>
        {
            var colis = plan.Colis;
            var row = colis.Row;
            var hash = Hash(RowHash(row), plan.ClientId, plan.CourierId, plan.AgencyId, plan.OriginCityId, plan.DestinationCityId, plan.DistrictId, plan.CurrentCityId,
                plan.Billing, plan.Financial, plan.Kind, plan.Reimbursed, plan.ReimbursedAt, plan.Commission, plan.CommissionCourierId, plan.ExternalCode,
                NewId("parcel", V(row, "reference_id")), context.DefaultDeliveryTypeId);
            var existingId = NewId("parcel", colis.Id);
            if (existingId is not null && mappings[("parcel", colis.Id)].Hash == hash)
            {
                report.Unchanged++;
                return;
            }
            var parcel = existingId is null ? null : await db.Parcels.FirstOrDefaultAsync(p => p.Id == existingId, cancellationToken);
            parcel ??= await db.Parcels.FirstOrDefaultAsync(p => p.Code == colis.Code, cancellationToken);
            if (parcel is null)
            {
                parcel = new Parcel { Code = colis.Code };
                db.Parcels.Add(parcel);
            }
            var createdAt = At(V(row, "created_at")) ?? DateTime.UtcNow;
            parcel.ExternalCode = plan.ExternalCode;
            parcel.ClientId = plan.ClientId;
            parcel.CourierId = plan.CourierId;
            parcel.AgencyId = plan.AgencyId;
            parcel.StatusId = colis.Status;
            parcel.StatusAt = At(V(row, "statut_date")) ?? At(V(row, "updated_at")) ?? createdAt;
            parcel.BillingState = plan.Billing;
            parcel.FinancialStatus = plan.Financial;
            parcel.OriginCityId = plan.OriginCityId;
            parcel.DestinationCityId = plan.DestinationCityId;
            parcel.DestinationDistrictId = plan.DistrictId;
            parcel.DestinationDistrictName = plan.DistrictName;
            parcel.DeliveryMode = DeliveryMode.DISTRICT;
            parcel.DeliveryTypeId = V(row, "type_livraison_id") is null ? null : context.DefaultDeliveryTypeId;
            parcel.CurrentCityId = plan.CurrentCityId;
            parcel.Description = V(row, "designation") ?? string.Empty;
            parcel.RecipientName = V(row, "destinataire") ?? string.Empty;
            parcel.RecipientPhone = V(row, "tel_destinataire") ?? string.Empty;
            parcel.PickupAddress = V(row, "ramassage_adresse") ?? string.Empty;
            parcel.DeliveryAddress = V(row, "livraison_adresse") ?? string.Empty;
            parcel.CodAmount = colis.Prix;
            parcel.DeliveryFee = colis.Frais;
            parcel.FailureFee = colis.FraisEchoue;
            parcel.OtherFees = 0m;
            parcel.FeeIncluded = Is(V(row, "frais_inclus"));
            parcel.AlreadyPaid = Is(V(row, "deja_paye"));
            parcel.PickupWaived = Is(V(row, "ramassage_exonere"));
            parcel.CourierCommission = plan.Commission;
            parcel.CourierCommissionAt = plan.CommissionAt;
            parcel.CourierCommissionSource = plan.Commission is null ? null : CourierCommissionSource.GRID;
            parcel.CourierCommissionCourierId = plan.CommissionCourierId;
            parcel.ReimbursedByCourier = plan.Reimbursed;
            parcel.ReimbursedAt = plan.ReimbursedAt;
            parcel.Kind = plan.Kind;
            parcel.IsReturn = Is(V(row, "retour")) || plan.Kind == ParcelKind.RETURN;
            parcel.IsFailed = Is(V(row, "colis_echoue"));
            parcel.ReferenceParcelId = NewId("parcel", V(row, "reference_id"));
            parcel.OpenOnDelivery = Is(V(row, "ouverture_colis"));
            parcel.Fragile = Is(V(row, "colis_fragile"));
            parcel.Quantity = 1;
            parcel.ScheduledAt = Day(V(row, "date_report"));
            parcel.Latitude = Coordinate(V(row, "latitude"), 90m);
            parcel.Longitude = Coordinate(V(row, "longitude"), 180m);
            parcel.CreatedAt = createdAt;
            parcel.UpdatedAt = At(V(row, "updated_at")) ?? createdAt;
            parcel.DeletedAt = null;
            Count(report, Track("parcel", colis.Id, parcel.Id, hash));
        }, cancellationToken);
    }

    private const string ExchangeCodePrefix = "ECH-";
    private const string ReturnCodePrefix = "RET-";

    private static decimal? Coordinate(string? value, decimal bound) =>
        value is not null && decimal.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && Math.Abs(d) <= bound ? Math.Round(d, 7) : null;

    // ------------------------------------------------------------------ historique

    private async Task EventsAsync(Source source, Context context, ImportOptions options, List<TableReport> tables, CancellationToken cancellationToken)
    {
        var report = Add(tables, "colis_trakings");
        var rows = source.Rows("colis_trakings")
            .OrderBy(r => V(r, "colis_id")).ThenBy(r => At(V(r, "created_at"))).ThenBy(r => int.Parse(V(r, "id")!, CultureInfo.InvariantCulture)).ToList();
        var plans = new List<(Dictionary<string, string?> Row, string ParcelId, int StatusId, bool Inherited)>();
        string? previousColis = null;
        var running = 1;
        var inherited = 0;
        foreach (var row in rows)
        {
            report.Read++;
            var colisId = V(row, "colis_id");
            if (colisId != previousColis)
            {
                previousColis = colisId;
                running = 1;
            }
            var parcelId = NewId("parcel", colisId);
            if (parcelId is null)
            {
                report.Ignore("colis non repris", $"{V(row, "id")} colis {colisId}");
                continue;
            }
            var hasStatus = int.TryParse(V(row, "statut_id"), CultureInfo.InvariantCulture, out var status);
            if (hasStatus) running = NearestStatus.TryGetValue(status, out var near) ? near : status;
            else inherited++;
            plans.Add((row, parcelId, running, !hasStatus));
        }
        if (inherited > 0) report.Notes.Add($"{inherited} ligne(s) d'historique sans statut (commentaire seul) : reprises avec le statut du colis à ce moment.");
        await BatchesAsync(plans, options, async plan =>
        {
            var row = plan.Row;
            var legacyId = V(row, "id")!;
            var authorId = NewId("user", V(row, "user_id"));
            var hash = Hash(RowHash(row), plan.ParcelId, plan.StatusId, authorId);
            var existingId = NewId("parcel_event", legacyId);
            if (existingId is not null && mappings[("parcel_event", legacyId)].Hash == hash)
            {
                report.Unchanged++;
                return;
            }
            var item = existingId is null ? null : await db.ParcelEvents.FirstOrDefaultAsync(e => e.Id == existingId, cancellationToken);
            if (item is null)
            {
                item = new ParcelEvent();
                db.ParcelEvents.Add(item);
            }
            item.ParcelId = plan.ParcelId;
            item.StatusId = plan.StatusId;
            item.Comment = V(row, "commentaire");
            item.AuthorId = authorId;
            item.AuthorName = UserName(context, V(row, "user_id"));
            item.AttachmentName = V(row, "file");
            item.ScheduledDate = Day(V(row, "date_report"));
            item.CreatedAt = At(V(row, "created_at")) ?? DateTime.UtcNow;
            Count(report, Track("parcel_event", legacyId, item.Id, hash));
        }, cancellationToken);
    }

    // ------------------------------------------------------------------ bons de ramassage et de dépôt

    private async Task DeliveryNotesAsync(Source source, Context context, ImportOptions options, List<TableReport> tables, CancellationToken cancellationToken)
    {
        var report = Add(tables, "bon_livraisons");
        var links = source.Rows("bon_livraison_colis").GroupBy(l => V(l, "bon_livraison_id")!).ToDictionary(g => g.Key, g => g.Select(l => V(l, "colis_id")!).ToList());
        var plans = new List<(Dictionary<string, string?> Row, string ClientId, List<string> Parcels)>();
        foreach (var row in source.Rows("bon_livraisons"))
        {
            report.Read++;
            var clientId = NewId("client", V(row, "client_id"));
            if (clientId is null)
            {
                report.Ignore("vendeur non repris", $"{V(row, "id")} {V(row, "code")}");
                continue;
            }
            var parcels = links.GetValueOrDefault(V(row, "id")!, []).Select(c => NewId("parcel", c)).OfType<string>().Distinct().ToList();
            plans.Add((row, clientId, parcels));
        }
        await BatchesAsync(plans, options, async plan =>
        {
            var row = plan.Row;
            var legacyId = V(row, "id")!;
            var validated = V(row, "statut_id") == "2";
            var city = (V(row, "emplacement_type") == "App\\Ville" ? NewId("city", V(row, "emplacement_id")) : null) ?? context.Clients.GetValueOrDefault(plan.ClientId).CityId;
            var hash = Hash(RowHash(row), plan.ClientId, city, string.Join(',', plan.Parcels));
            var existingId = NewId("delivery_note", legacyId);
            if (existingId is not null && mappings[("delivery_note", legacyId)].Hash == hash)
            {
                report.Unchanged++;
                return;
            }
            var note = existingId is null ? null : await db.DeliveryNotes.FirstOrDefaultAsync(n => n.Id == existingId, cancellationToken);
            if (note is null)
            {
                note = new DeliveryNote { Code = V(row, "code")! };
                db.DeliveryNotes.Add(note);
            }
            else await db.DeliveryNoteParcels.Where(l => l.DeliveryNoteId == note.Id).ExecuteDeleteAsync(cancellationToken);
            var createdAt = At(V(row, "created_at")) ?? DateTime.UtcNow;
            note.Type = V(row, "type") == "ramassage" ? DeliveryNoteType.PICKUP : DeliveryNoteType.DROPOFF;
            note.Status = validated ? DeliveryNoteStatus.VALIDATED : DeliveryNoteStatus.AWAITING_VALIDATION;
            note.ClientId = plan.ClientId;
            note.AgencyId = NewId("agency", V(row, "sys_agence")) ?? context.Clients.GetValueOrDefault(plan.ClientId).AgencyId ?? context.DefaultAgencyId;
            note.LocationCityId = city!;
            note.ValidatedAt = validated ? At(V(row, "updated_at")) ?? createdAt : null;
            note.CreatedAt = createdAt;
            note.UpdatedAt = At(V(row, "updated_at")) ?? createdAt;
            note.ImportedAt = validated ? DateTime.UtcNow : null;
            foreach (var parcelId in plan.Parcels) db.DeliveryNoteParcels.Add(new DeliveryNoteParcel { DeliveryNoteId = note.Id, ParcelId = parcelId });
            Count(report, Track("delivery_note", legacyId, note.Id, hash));
        }, cancellationToken);
    }

    // ------------------------------------------------------------------ circuits

    /// <summary>Un colis encore en mouvement sur son dernier circuit : le circuit reste ouvert dans .NET.</summary>
    private static bool InProgress(int status) =>
        StatusCatalog.All.FirstOrDefault(s => s.Id == status)?.Group is "pivot" or "retry" or "transit" or "initial";

    private async Task RoutesAsync(Source source, Context context, ImportOptions options, List<TableReport> tables, CancellationToken cancellationToken)
    {
        var report = Add(tables, "boxes");
        var links = source.Rows("boxe_colis").OrderBy(l => int.Parse(V(l, "id")!, CultureInfo.InvariantCulture)).GroupBy(l => V(l, "boxe_id")!).ToDictionary(g => g.Key, g => g.ToList());
        var plans = new List<(Dictionary<string, string?> Row, string CourierId, RouteStatus Status, List<(Dictionary<string, string?> Link, string ParcelId, Colis Colis)> Parcels)>();
        var kept = 0;
        foreach (var row in source.Rows("boxes"))
        {
            report.Read++;
            var label = $"{V(row, "id")} {V(row, "code")}";
            var courierId = V(row, "destinataire_type") == "App\\Livreur" ? NewId("courier", V(row, "destinataire_id")) : null;
            if (courierId is null)
            {
                report.Ignore("livreur non repris", label);
                continue;
            }
            if (NewId("city", V(row, "ville_id")) is null)
            {
                report.Ignore("ville non reprise", label);
                continue;
            }
            var parcels = links.GetValueOrDefault(V(row, "id")!, [])
                .Select(l => (Link: l, ParcelId: NewId("parcel", V(l, "colis_id")), Colis: context.Colis.GetValueOrDefault(V(l, "colis_id")!)))
                .Where(x => x.ParcelId is not null && x.Colis is not null).Select(x => (Link: x.Link, ParcelId: x.ParcelId!, Colis: x.Colis!)).ToList();
            var legacyStatus = V(row, "statut_id");
            var live = parcels.Any(p => context.LastBoxOfColis.GetValueOrDefault(p.Colis.Id) == V(row, "id") && InProgress(p.Colis.Status));
            var status = legacyStatus switch
            {
                "5" => RouteStatus.DRAFT,
                "7" => RouteStatus.DELIVERING,
                "8" => RouteStatus.COLLECTING,
                "9" => RouteStatus.CLOSED,
                _ => live ? RouteStatus.DISPATCHED : RouteStatus.CLOSED,
            };
            if (legacyStatus == "6" && live) kept++;
            plans.Add((row, courierId, status, parcels));
        }
        report.Notes.Add($"circuits « Dispatched » : {kept} gardés ouverts (un colis y est encore en cours), les autres clos (tous leurs colis sont arrivés au bout).");
        await BatchesAsync(plans, options, async plan =>
        {
            var row = plan.Row;
            var legacyId = V(row, "id")!;
            var lines = plan.Parcels.Select((p, index) =>
            {
                var last = context.LastBoxOfColis.GetValueOrDefault(p.Colis.Id) == legacyId && !InProgress(p.Colis.Status);
                return new RouteParcel
                {
                    ParcelId = p.ParcelId, Sequence = index + 1, Confirmed = Is(V(p.Link, "confirmed")), ConfirmedAt = At(V(p.Link, "confirmed_date")),
                    ConfirmedBy = NewId("user", V(p.Link, "confirmed_user")), FinalStatusId = last ? p.Colis.Status : null,
                    CodCollected = last && p.Colis.Delivered ? p.Colis.MontantAEncaisser : null, CodCash = last && p.Colis.Delivered ? p.Colis.MontantAEncaisser : null,
                };
            }).ToList();
            var hash = Hash(RowHash(row), plan.CourierId, plan.Status, string.Join(';', lines.Select(l => $"{l.ParcelId}|{l.Confirmed}|{l.ConfirmedAt:O}|{l.ConfirmedBy}|{l.FinalStatusId}|{l.CodCollected}")));
            var existingId = NewId("route", legacyId);
            if (existingId is not null && mappings[("route", legacyId)].Hash == hash)
            {
                report.Unchanged++;
                return;
            }
            var route = existingId is null ? null : await db.Routes.FirstOrDefaultAsync(r => r.Id == existingId, cancellationToken);
            if (route is null)
            {
                route = new Route { Code = V(row, "code")! };
                db.Routes.Add(route);
            }
            else await db.RouteParcels.Where(l => l.RouteId == route.Id).ExecuteDeleteAsync(cancellationToken);
            var createdAt = At(V(row, "created_at")) ?? DateTime.UtcNow;
            route.Status = plan.Status;
            route.StatusAt = At(V(row, "statut_date")) ?? At(V(row, "updated_at")) ?? createdAt;
            route.Kind = V(row, "type_circuit") == "ramassage" || Is(V(row, "ramassage")) ? RouteKind.PICKUP : RouteKind.DELIVERY;
            route.CityId = NewId("city", V(row, "ville_id"))!;
            route.OriginCityId = NewId("city", V(row, "depart_id")) ?? route.CityId;
            route.CourierId = plan.CourierId;
            route.ExitDate = Day(V(row, "date_sorti"));
            route.CreatedAt = createdAt;
            route.UpdatedAt = At(V(row, "updated_at")) ?? createdAt;
            route.ImportedAt = plan.Status == RouteStatus.CLOSED ? DateTime.UtcNow : null;
            foreach (var line in lines)
            {
                line.RouteId = route.Id;
                db.RouteParcels.Add(line);
            }
            Count(report, Track("route", legacyId, route.Id, hash));
        }, cancellationToken);
    }

    // ------------------------------------------------------------------ factures clients

    private async Task InvoicesAsync(Source source, Context context, ImportOptions options, List<TableReport> tables, CancellationToken cancellationToken)
    {
        var report = Add(tables, "factures (clients)");
        var links = source.Rows("facture_colis").OrderBy(l => int.Parse(V(l, "id")!, CultureInfo.InvariantCulture)).GroupBy(l => V(l, "facture_id")!)
            .ToDictionary(g => g.Key, g => g.Select(l => V(l, "colis_id")!).Distinct().ToList());
        var payments = source.Rows("rembourssements").GroupBy(p => V(p, "facture_id") ?? "").ToDictionary(g => g.Key, g => g.ToList());
        var plans = new List<(Dictionary<string, string?> Row, string ClientId, List<(Colis Colis, string ParcelId)> Lines)>();
        var deletedLines = 0;
        foreach (var row in context.Factures.Values.Where(f => V(f, "client_id") is not null))
        {
            report.Read++;
            var clientId = NewId("client", V(row, "client_id"));
            if (clientId is null)
            {
                report.Ignore("vendeur non repris", $"{V(row, "id")} {V(row, "code")}");
                continue;
            }
            var lines = new List<(Colis, string)>();
            foreach (var colisId in links.GetValueOrDefault(V(row, "id")!, []))
            {
                // `App\Facture::colis()` filtre `deleted_at` : un colis supprimé ne compte pas dans Laravel non plus.
                if (!context.Colis.TryGetValue(colisId, out var colis) || Deleted(colis.Row)) { deletedLines++; continue; }
                if (NewId("parcel", colisId) is not { } parcelId)
                {
                    report.Notes.Add($"facture {V(row, "code")} : colis {colis.Code} non repris — sa ligne manque.");
                    continue;
                }
                lines.Add((colis, parcelId));
            }
            plans.Add((row, clientId, lines));
        }
        if (deletedLines > 0) report.Notes.Add($"{deletedLines} ligne(s) de facture vers un colis supprimé : ignorées, comme dans les totaux Laravel.");
        await BatchesAsync(plans, options, async plan =>
        {
            var row = plan.Row;
            var legacyId = V(row, "id")!;
            var status = V(row, "statut_id") switch { "3" => InvoiceStatus.PAID, "2" => InvoiceStatus.ISSUED, _ => InvoiceStatus.DRAFT };
            var amounts = plan.Lines.Select(l => (l.Colis, l.ParcelId, Amount: InvoiceLineOf(l.Colis))).ToList();
            var paidAt = payments.GetValueOrDefault(legacyId, []).Select(p => Day(V(p, "date_paiement")) ?? At(V(p, "created_at"))).Max();
            var hash = Hash(RowHash(row), plan.ClientId, paidAt,
                string.Join(';', amounts.Select(a => $"{a.ParcelId}|{a.Colis.Status}|{a.Amount.Cod}|{a.Amount.Fee}|{a.Amount.Failure}|{a.Amount.Net}")));
            var existingId = NewId("invoice", legacyId);
            if (existingId is not null && mappings[("invoice", legacyId)].Hash == hash)
            {
                report.Unchanged++;
                return;
            }
            var invoice = existingId is null ? null : await db.Invoices.FirstOrDefaultAsync(i => i.Id == existingId, cancellationToken);
            if (invoice is null)
            {
                invoice = new Invoice { Code = V(row, "code")! };
                db.Invoices.Add(invoice);
            }
            else await db.InvoiceLines.Where(l => l.InvoiceId == invoice.Id).ExecuteDeleteAsync(cancellationToken);
            var createdAt = At(V(row, "created_at")) ?? DateTime.UtcNow;
            var updatedAt = At(V(row, "updated_at")) ?? createdAt;
            invoice.Status = status;
            invoice.ClientId = plan.ClientId;
            invoice.AgencyId = NewId("agency", V(row, "sys_agence")) ?? context.Clients.GetValueOrDefault(plan.ClientId).AgencyId ?? context.DefaultAgencyId;
            invoice.Note = ImportNote;
            invoice.TotalCod = amounts.Sum(a => a.Amount.Cod);
            invoice.TotalDeliveryFee = amounts.Sum(a => a.Amount.Fee);
            invoice.TotalFailureFee = amounts.Sum(a => a.Amount.Failure);
            invoice.TotalVat = 0m;
            invoice.TotalAdjustment = 0m;
            invoice.TotalDue = amounts.Sum(a => a.Amount.Net);
            invoice.VatRate = 0;
            invoice.BillingType = BillingType.NORMAL;
            invoice.IssuedAt = status == InvoiceStatus.DRAFT ? null : updatedAt;
            invoice.PaidAt = status == InvoiceStatus.PAID ? paidAt ?? updatedAt : null;
            invoice.CreatedAt = createdAt;
            invoice.UpdatedAt = updatedAt;
            invoice.ImportedAt = status == InvoiceStatus.DRAFT ? null : DateTime.UtcNow;
            foreach (var (colis, parcelId, amount) in amounts)
            {
                db.InvoiceLines.Add(new InvoiceLine
                {
                    InvoiceId = invoice.Id, ParcelId = parcelId, ParcelCode = colis.Code, StatusId = colis.Status, CodAmount = amount.Cod, DeliveryFee = amount.Fee,
                    FailureFee = amount.Failure, VatAmount = 0m, ChequePaid = 0m, NetDue = amount.Net, InvoicedParcelId = status == InvoiceStatus.DRAFT ? null : parcelId,
                });
            }
            Count(report, Track("invoice", legacyId, invoice.Id, hash));
        }, cancellationToken);
    }

    private async Task PaymentsAsync(Source source, Context context, ImportOptions options, List<TableReport> tables, CancellationToken cancellationToken)
    {
        var report = Add(tables, "rembourssements");
        var plans = new List<(Dictionary<string, string?> Row, string InvoiceId, string ClientId)>();
        var invoiceClients = await db.Invoices.AsNoTracking().Where(i => i.ImportedAt != null || i.Note == ImportNote).Select(i => new { i.Id, i.ClientId, i.AgencyId })
            .ToDictionaryAsync(i => i.Id, cancellationToken);
        foreach (var row in source.Rows("rembourssements"))
        {
            report.Read++;
            var label = $"{V(row, "id")} facture {V(row, "facture_id")}";
            if (V(row, "livreur_id") is not null && V(row, "client_id") is null)
            {
                report.Ignore("règlement d'une facture livreur (porté par le relevé)", label);
                continue;
            }
            if (NewId("invoice", V(row, "facture_id")) is not { } invoiceId || !invoiceClients.TryGetValue(invoiceId, out var invoice))
            {
                report.Ignore("facture non reprise", label);
                continue;
            }
            plans.Add((row, invoiceId, NewId("client", V(row, "client_id")) ?? invoice.ClientId));
        }
        await BatchesAsync(plans, options, async plan =>
        {
            var row = plan.Row;
            var legacyId = V(row, "id")!;
            var hash = Hash(RowHash(row), plan.InvoiceId, plan.ClientId, NewId("bank_account", V(row, "compte_id")));
            var existingId = NewId("payment", legacyId);
            if (existingId is not null && mappings[("payment", legacyId)].Hash == hash)
            {
                report.Unchanged++;
                return;
            }
            var payment = existingId is null ? null : await db.Payments.FirstOrDefaultAsync(p => p.Id == existingId, cancellationToken);
            if (payment is null)
            {
                payment = new Payment();
                db.Payments.Add(payment);
            }
            var createdAt = At(V(row, "created_at")) ?? DateTime.UtcNow;
            payment.InvoiceId = plan.InvoiceId;
            payment.ClientId = plan.ClientId;
            payment.AgencyId = invoiceClients[plan.InvoiceId].AgencyId;
            payment.Amount = D(V(row, "montant"));
            payment.Method = V(row, "mode_id") switch { "2" => PaymentMethod.TRANSFER, "3" => PaymentMethod.CHECK, _ => PaymentMethod.CASH };
            payment.BankAccountId = NewId("bank_account", V(row, "compte_id"));
            payment.Reference = V(row, "reference");
            payment.Note = V(row, "commentaire") is { } comment && comment != "null" ? comment : ImportNote;
            payment.PaidAt = Day(V(row, "date_paiement")) ?? createdAt;
            payment.ProofName = V(row, "file");
            payment.CreatedAt = createdAt;
            payment.UpdatedAt = At(V(row, "updated_at")) ?? createdAt;
            payment.ImportedAt = DateTime.UtcNow;
            Count(report, Track("payment", legacyId, payment.Id, hash));
        }, cancellationToken);
    }

    // ------------------------------------------------------------------ relevés (factures livreurs)

    private async Task StatementsAsync(Source source, Context context, ImportOptions options, List<TableReport> tables, CancellationToken cancellationToken)
    {
        var report = Add(tables, "factures (livreurs)");
        var trakings = source.Rows("colis_trakings").ToDictionary(r => V(r, "id")!);
        var links = source.Rows("facture_livreur_colis").GroupBy(l => V(l, "facture_id")!).ToDictionary(g => g.Key, g => g.Select(l => V(l, "traking_id")!).Distinct().ToList());
        var cities = await db.Cities.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        var plans = new List<(Dictionary<string, string?> Row, string CourierId, List<(Dictionary<string, string?> Traking, string ParcelId, Colis Colis)> Lines)>();
        foreach (var row in context.Factures.Values.Where(f => V(f, "livreur_id") is not null && V(f, "client_id") is null))
        {
            report.Read++;
            var courierId = NewId("courier", V(row, "livreur_id"));
            if (courierId is null)
            {
                report.Ignore("livreur non repris", $"{V(row, "id")} {V(row, "code")}");
                continue;
            }
            var lines = links.GetValueOrDefault(V(row, "id")!, []).Select(t => trakings.GetValueOrDefault(t)).OfType<Dictionary<string, string?>>()
                .Select(t => (Traking: t, ParcelId: NewId("parcel", V(t, "colis_id")), Colis: context.Colis.GetValueOrDefault(V(t, "colis_id") ?? "")))
                .Where(x => x.ParcelId is not null && x.Colis is not null).Select(x => (Traking: x.Traking, ParcelId: x.ParcelId!, Colis: x.Colis!)).ToList();
            plans.Add((row, courierId, lines));
        }
        await BatchesAsync(plans, options, async plan =>
        {
            var row = plan.Row;
            var legacyId = V(row, "id")!;
            var status = V(row, "statut_id") switch { "3" => CourierStatementStatus.PAID, "2" => CourierStatementStatus.VALIDATED, _ => CourierStatementStatus.DRAFT };
            var hash = Hash(RowHash(row), plan.CourierId, string.Join(';', plan.Lines.Select(l => RowHash(l.Traking))));
            var existingId = NewId("courier_statement", legacyId);
            if (existingId is not null && mappings[("courier_statement", legacyId)].Hash == hash)
            {
                report.Unchanged++;
                return;
            }
            var statement = existingId is null ? null : await db.CourierStatements.FirstOrDefaultAsync(s => s.Id == existingId, cancellationToken);
            if (statement is null)
            {
                statement = new CourierStatement { Code = V(row, "code")! };
                db.CourierStatements.Add(statement);
            }
            else await db.CourierStatementLines.Where(l => l.StatementId == statement.Id).ExecuteDeleteAsync(cancellationToken);
            var createdAt = At(V(row, "created_at")) ?? DateTime.UtcNow;
            var updatedAt = At(V(row, "updated_at")) ?? createdAt;
            var worked = plan.Lines.Select(l => At(V(l.Traking, "created_at")) ?? createdAt).DefaultIfEmpty(createdAt).ToList();
            var total = plan.Lines.Sum(l => D(V(l.Traking, "frais_livreur")));
            statement.Status = status;
            statement.CourierId = plan.CourierId;
            statement.AgencyId = context.Couriers.GetValueOrDefault(plan.CourierId).AgencyId ?? context.DefaultAgencyId;
            statement.PeriodStart = worked.Min();
            statement.PeriodEnd = worked.Max();
            statement.PeriodKind = PayPeriod.MONTHLY;
            statement.PayModel = PayModel.COMMISSION;
            statement.SettlementMode = SettlementMode.MONTHLY;
            statement.BaseAmount = 0m;
            statement.CodCollected = 0m;
            statement.CommissionsTotal = total;
            statement.CommissionsDeducted = 0m;
            statement.VariableAmount = total;
            statement.TotalDue = total;
            statement.Note = ImportNote;
            statement.ValidatedAt = status == CourierStatementStatus.DRAFT ? null : updatedAt;
            statement.PaidAt = status == CourierStatementStatus.PAID ? updatedAt : null;
            statement.PaymentMethod = PaymentMethod.CASH;
            statement.CreatedAt = createdAt;
            statement.UpdatedAt = updatedAt;
            statement.ImportedAt = status == CourierStatementStatus.DRAFT ? null : DateTime.UtcNow;
            foreach (var (traking, parcelId, colis) in plan.Lines)
            {
                var kind = V(traking, "statut_id") == PickedUpByCourierStatus.ToString(CultureInfo.InvariantCulture) ? CourierStatementLineKind.PICKUP : CourierStatementLineKind.DELIVERY;
                db.CourierStatementLines.Add(new CourierStatementLine
                {
                    StatementId = statement.Id, ParcelId = parcelId, Kind = kind, ParcelCode = colis.Code,
                    CityName = NewId("city", V(colis.Row, "livraison_ville_id")) is { } city ? cities.GetValueOrDefault(city, string.Empty) : string.Empty,
                    StatusId = NearestStatus.TryGetValue(int.Parse(V(traking, "statut_id") ?? "4", CultureInfo.InvariantCulture), out var near) ? near : int.Parse(V(traking, "statut_id") ?? "4", CultureInfo.InvariantCulture),
                    Amount = D(V(traking, "frais_livreur")), Deducted = false, WorkedAt = At(V(traking, "created_at")) ?? createdAt,
                    SettledParcelId = status == CourierStatementStatus.DRAFT ? null : parcelId, CreatedAt = createdAt,
                });
            }
            Count(report, Track("courier_statement", legacyId, statement.Id, hash));
        }, cancellationToken);
    }

    // ------------------------------------------------------------------ caisses (recettes)

    private async Task RemittancesAsync(Source source, Context context, ImportOptions options, List<TableReport> tables, CancellationToken cancellationToken)
    {
        var report = Add(tables, "recettes");
        var links = source.Rows("recette_colis").OrderBy(l => int.Parse(V(l, "id")!, CultureInfo.InvariantCulture)).GroupBy(l => V(l, "recette_id")!)
            .ToDictionary(g => g.Key, g => g.Select(l => V(l, "colis_id")!).Distinct().ToList());
        var rows = source.Rows("recettes").OrderBy(r => int.Parse(V(r, "id")!, CultureInfo.InvariantCulture)).ToList();
        // Un seul retour de fonds ouvert par livreur dans .NET : le plus récent des brouillons Laravel le reste.
        var openOf = rows.Where(r => V(r, "confirmation_date") is null).GroupBy(r => V(r, "partenaire_id") ?? "").ToDictionary(g => g.Key, g => V(g.Last(), "id"));
        var plans = new List<(Dictionary<string, string?> Row, string CourierId, CashRemittanceStatus Status, List<(Colis Colis, string ParcelId, decimal Amount, decimal Commission, bool Confirms)> Lines)>();
        foreach (var row in rows)
        {
            report.Read++;
            var legacyId = V(row, "id")!;
            var label = $"{legacyId} {V(row, "code")}";
            var courierLegacy = V(row, "partenaire_id") ?? "";
            var courierId = V(row, "partenaire_type") == "App\\Livreur" ? NewId("courier", courierLegacy) : null;
            if (courierId is null)
            {
                report.Ignore("livreur non repris", label);
                continue;
            }
            var status = V(row, "confirmation_date") is null ? CashRemittanceStatus.DRAFT
                : V(row, "confirmation") == "1" ? CashRemittanceStatus.CONFIRMED : CashRemittanceStatus.CANCELLED;
            if (status == CashRemittanceStatus.DRAFT && openOf.GetValueOrDefault(courierLegacy) != legacyId)
            {
                status = CashRemittanceStatus.CANCELLED;
                report.Notes.Add($"{label} : brouillon plus ancien d'un livreur qui en a un autre → « Annulé » (un seul retour de fonds ouvert par livreur dans .NET).");
            }
            var grid = context.Grids.GetValueOrDefault(courierLegacy, []);
            var lines = new List<(Colis, string, decimal, decimal, bool)>();
            foreach (var colisId in links.GetValueOrDefault(legacyId, []))
            {
                if (!context.Colis.TryGetValue(colisId, out var colis) || Deleted(colis.Row) || NewId("parcel", colisId) is not { } parcelId) continue;
                // `LivreurFactureService::calculer` : seuls les colis livrés comptent (COD = `montantAEncaisser`, frais = grille du livreur).
                var amount = colis.Delivered ? colis.MontantAEncaisser : 0m;
                var commission = colis.Delivered && int.TryParse(V(colis.Row, "livraison_ville_id"), CultureInfo.InvariantCulture, out var city) ? grid.GetValueOrDefault(city) : 0m;
                var confirms = status == CashRemittanceStatus.CONFIRMED && colis.Delivered && context.FirstConfirmedRecette.GetValueOrDefault(colisId).Id == legacyId;
                lines.Add((colis, parcelId, amount, commission, confirms));
            }
            plans.Add((row, courierId, status, lines));
        }
        await BatchesAsync(plans, options, async plan =>
        {
            var row = plan.Row;
            var legacyId = V(row, "id")!;
            var hash = Hash(RowHash(row), plan.CourierId, plan.Status, string.Join(';', plan.Lines.Select(l => $"{l.ParcelId}|{l.Amount}|{l.Commission}|{l.Confirms}")));
            var existingId = NewId("cash_remittance", legacyId);
            if (existingId is not null && mappings[("cash_remittance", legacyId)].Hash == hash)
            {
                report.Unchanged++;
                return;
            }
            var remittance = existingId is null ? null : await db.CashRemittances.FirstOrDefaultAsync(r => r.Id == existingId, cancellationToken);
            if (remittance is null)
            {
                remittance = new CashRemittance { Code = V(row, "code")! };
                db.CashRemittances.Add(remittance);
            }
            else await db.CashRemittanceParcels.Where(l => l.RemittanceId == remittance.Id).ExecuteDeleteAsync(cancellationToken);
            var courier = context.Couriers.GetValueOrDefault(plan.CourierId);
            var createdAt = At(V(row, "created_at")) ?? DateTime.UtcNow;
            var cod = plan.Lines.Sum(l => l.Amount);
            var commission = plan.Lines.Sum(l => l.Commission);
            remittance.Status = plan.Status;
            remittance.Origin = RemittanceOrigin.MANUAL;
            remittance.CityId = NewId("city", V(row, "ville_id")) ?? courier.CityId;
            remittance.CourierId = plan.CourierId;
            remittance.AgencyId = courier.AgencyId ?? context.DefaultAgencyId;
            remittance.ExpectedCod = cod;
            remittance.ExpectedCommission = commission;
            remittance.ExpectedNet = cod - commission;
            remittance.Amount = cod - commission;
            remittance.TotalCash = cod;
            remittance.TotalCheque = 0m;
            remittance.Variance = 0m;
            remittance.DeclaredAmount = null;
            remittance.SettlementMode = SettlementMode.AT_REMITTANCE;
            remittance.PayModel = PayModel.COMMISSION;
            remittance.Note = V(row, "commentaire") is { } comment ? $"{ImportNote} — {comment}" : ImportNote;
            remittance.ProofName = V(row, "file");
            remittance.ConfirmedAt = plan.Status == CashRemittanceStatus.DRAFT ? null : At(V(row, "confirmation_date"));
            remittance.ConfirmedById = NewId("user", V(row, "confirmation_user"));
            remittance.CreatedAt = createdAt;
            remittance.UpdatedAt = At(V(row, "updated_at")) ?? createdAt;
            remittance.ImportedAt = plan.Status == CashRemittanceStatus.DRAFT ? null : DateTime.UtcNow;
            foreach (var line in plan.Lines)
            {
                db.CashRemittanceParcels.Add(new CashRemittanceParcel
                {
                    RemittanceId = remittance.Id, ParcelId = line.ParcelId, Amount = line.Amount, Cash = line.Amount, Cheque = 0m, Commission = line.Commission,
                    ConfirmedParcelId = line.Confirms ? line.ParcelId : null,
                });
            }
            await db.SaveChangesAsync(cancellationToken);
            var confirmed = plan.Lines.Where(l => l.Confirms).Select(l => l.ParcelId).ToList();
            if (confirmed.Count > 0)
            {
                await db.Parcels.Where(p => confirmed.Contains(p.Id)).ExecuteUpdateAsync(s => s.SetProperty(p => p.ReimbursedRemittanceId, remittance.Id), cancellationToken);
            }
            Count(report, Track("cash_remittance", legacyId, remittance.Id, hash));
        }, cancellationToken);
    }

    // ------------------------------------------------------------------ échanges

    private async Task ExchangesAsync(Context context, ImportOptions options, List<TableReport> tables, CancellationToken cancellationToken)
    {
        var report = Add(tables, "échanges (ECH-)");
        var plans = new List<(Colis New, Colis Original, string NewId, string OriginalId, Colis? Return)>();
        var returns = context.Colis.Values.Where(c => c.Code.StartsWith(ReturnCodePrefix, StringComparison.Ordinal) && !Deleted(c.Row))
            .GroupBy(c => V(c.Row, "reference_id") ?? "").ToDictionary(g => g.Key, g => g.First());
        foreach (var colis in context.Colis.Values.Where(c => c.Code.StartsWith(ExchangeCodePrefix, StringComparison.Ordinal)))
        {
            report.Read++;
            var reference = V(colis.Row, "reference_id");
            if (NewId("parcel", colis.Id) is not { } newId || reference is null || NewId("parcel", reference) is not { } originalId || !context.Colis.TryGetValue(reference, out var original))
            {
                report.Ignore("colis d'échange ou colis d'origine non repris", $"{colis.Id} {colis.Code}");
                continue;
            }
            plans.Add((colis, original, newId, originalId, returns.GetValueOrDefault(reference)));
        }
        await BatchesAsync(plans, options, async plan =>
        {
            var status = plan.New.Status is 19 or 27 ? ExchangeStatus.ANNULE
                : plan.New.Delivered && plan.Return is { Status: 23 } ? ExchangeStatus.CLOTURE
                : plan.New.Delivered ? ExchangeStatus.NOUVEAU_LIVRE
                : plan.Return is { Status: 22 or 23 } ? ExchangeStatus.ANCIEN_RECUPERE
                : ExchangeStatus.NOUVEAU_COLIS_CREE;
            var hash = Hash(RowHash(plan.New.Row), RowHash(plan.Original.Row), plan.Return is null ? null : RowHash(plan.Return.Row), status);
            var existingId = NewId("exchange", plan.New.Id);
            if (existingId is not null && mappings[("exchange", plan.New.Id)].Hash == hash)
            {
                report.Unchanged++;
                return;
            }
            var exchange = existingId is null ? null : await db.Exchanges.FirstOrDefaultAsync(e => e.Id == existingId, cancellationToken);
            if (exchange is null)
            {
                exchange = new Exchange { Code = plan.New.Code };
                db.Exchanges.Add(exchange);
            }
            var requestedAt = At(V(plan.New.Row, "created_at")) ?? DateTime.UtcNow;
            var statusAt = At(V(plan.New.Row, "statut_date")) ?? At(V(plan.New.Row, "updated_at")) ?? requestedAt;
            exchange.Status = status;
            exchange.OriginalParcelId = plan.OriginalId;
            exchange.NewParcelId = plan.NewId;
            exchange.ReturnedProduct = V(plan.Original.Row, "designation") ?? string.Empty;
            exchange.NewProduct = V(plan.New.Row, "designation") ?? string.Empty;
            exchange.OldDestination = V(plan.Original.Row, "livraison_adresse") ?? string.Empty;
            exchange.NewDestination = V(plan.New.Row, "livraison_adresse") ?? string.Empty;
            exchange.Amount = plan.New.Prix;
            exchange.Fees = plan.New.Frais;
            exchange.CourierId = NewId("courier", V(plan.New.Row, "livreur_id"));
            exchange.RequestedAt = requestedAt;
            exchange.DeliveredAt = plan.New.Delivered ? statusAt : null;
            exchange.ClosedAt = status == ExchangeStatus.CLOTURE ? statusAt : null;
            exchange.CancelledAt = status == ExchangeStatus.ANNULE ? statusAt : null;
            exchange.Note = ImportNote;
            exchange.CreatedAt = requestedAt;
            exchange.UpdatedAt = statusAt;
            Count(report, Track("exchange", plan.New.Id, exchange.Id, hash));
        }, cancellationToken);
    }

    // ------------------------------------------------------------------ compteurs

    private static readonly System.Text.RegularExpressions.Regex ParcelCodePattern = new(@"^([A-Z]{3})(\d{2})(\d{3,})(\d{2})(\d{2})([A-Z]{3})$");

    /// <summary>
    /// `parcel_code_sequences` après le plus grand rang importé par (jour, départ, arrivée) : les codes Laravel ont le
    /// format .NET (`buildParcelCode`). Les factures, circuits, bons et recettes Laravel portent un code aléatoire de 10
    /// caractères, sans numéro : aucun compteur `document_sequences` n'est concerné.
    /// </summary>
    private async Task CountersAsync(Context context, ImportOptions options, List<TableReport> tables, CancellationToken cancellationToken)
    {
        var report = Add(tables, "compteurs");
        var cities = await db.Cities.AsNoTracking().GroupBy(c => c.Code).Select(g => new { Code = g.Key, g.First().Id }).ToDictionaryAsync(c => c.Code, c => c.Id, cancellationToken);
        var ranks = new Dictionary<(DateOnly, string, string), int>();
        foreach (var colis in context.Colis.Values)
        {
            var match = ParcelCodePattern.Match(colis.Code);
            if (!match.Success || !cities.TryGetValue(match.Groups[1].Value, out var origin) || !cities.TryGetValue(match.Groups[6].Value, out var destination)) continue;
            int day = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture), year = 2000 + int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture),
                month = int.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture), rank = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month)) continue;
            var key = (new DateOnly(year, month, day), origin, destination);
            ranks[key] = Math.Max(ranks.GetValueOrDefault(key), rank);
        }
        await InBatchAsync(options, async () =>
        {
            foreach (var ((day, origin, destination), rank) in ranks)
            {
                report.Read++;
                var moved = await db.Database.ExecuteSqlAsync($"""
                    INSERT INTO "parcel_code_sequences" ("day", "originCityId", "destinationCityId", "lastRank")
                    VALUES ({day}, {origin}, {destination}, {rank})
                    ON CONFLICT ("day", "originCityId", "destinationCityId")
                    DO UPDATE SET "lastRank" = GREATEST("parcel_code_sequences"."lastRank", EXCLUDED."lastRank")
                    """, cancellationToken);
                if (moved > 0) report.Imported++;
            }
        }, cancellationToken);
        report.Notes.Add($"{ranks.Count} compteur(s) de colis (jour, départ, arrivée) au plus grand rang importé ; factures, circuits, bons et recettes : codes Laravel aléatoires, aucun compteur à repositionner.");
    }
}
