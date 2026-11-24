using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Parcels.Import;

/// <summary>`ImportRowError` : ligne, colonne (absente pour une ligne vide), code et message.</summary>
public sealed record ImportRowError(int Row, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Column, string Code, string Message);

/// <summary>`ParcelImportRow` : une ligne lue, ses erreurs, ses avertissements, son frais.</summary>
public sealed record ParcelImportRowDto(int Row, IReadOnlyDictionary<string, string> Values, IReadOnlyList<ImportRowError> Errors)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ImportRowError>? Warnings { get; init; }

    public long? Fee { get; init; }
}

/// <summary>`parcelImportRowSchema` : une ligne saisie à l'écran (cellules en texte).</summary>
public sealed record ParcelImportRowInput
{
    public string? ExternalCode { get; init => field = Zod.Trim(value); }
    public string? RecipientName { get; init => field = Zod.Trim(value); }
    public string? RecipientPhone { get; init => field = Zod.Trim(value); }
    public string? City { get; init => field = Zod.Trim(value); }
    public string? District { get; init => field = Zod.Trim(value); }
    public string? Address { get; init => field = Zod.Trim(value); }
    public string? Cod { get; init => field = Zod.Trim(value); }
    public string? Description { get; init => field = Zod.Trim(value); }
    public string? OpenOnDelivery { get; init => field = Zod.Trim(value); }
    public string? Fragile { get; init => field = Zod.Trim(value); }
    public string? Notes { get; init => field = Zod.Trim(value); }
    public string? Quantity { get; init => field = Zod.Trim(value); }
    public double? Row { get; init; }

    public Dictionary<string, string> Cells() => new()
    {
        ["externalCode"] = ExternalCode ?? string.Empty,
        ["recipientName"] = RecipientName ?? string.Empty,
        ["recipientPhone"] = RecipientPhone ?? string.Empty,
        ["city"] = City ?? string.Empty,
        ["district"] = District ?? string.Empty,
        ["address"] = Address ?? string.Empty,
        ["cod"] = Cod ?? string.Empty,
        ["description"] = Description ?? string.Empty,
        ["openOnDelivery"] = OpenOnDelivery ?? string.Empty,
        ["fragile"] = Fragile ?? string.Empty,
        ["notes"] = Notes ?? string.Empty,
        ["quantity"] = Quantity ?? string.Empty,
    };
}

/// <summary>`ParcelImportService` : contexte du vendeur, lecture, validation ligne à ligne, écriture.</summary>
public static class ParcelImports
{
    public const int MaxRows = 2000;
    private const int AddressShort = 6;
    private const int PhoneDigitsMin = 9;
    private static readonly Regex LetterOrDigit = new(@"[\p{L}\p{N}]", RegexOptions.Compiled);
    private static readonly Regex Digits = new(@"^\d+$", RegexOptions.Compiled);

    public sealed record Context(string ClientId, string ClientName, string OriginCityId, string OriginCityName, string PickupAddress, string AgencyId, bool IsPro);

    public sealed record ReadRow(int Row, Dictionary<string, string> Values);

    public sealed record ReadyRow(int Row, string? ExternalCode, string RecipientName, string RecipientPhone, string DestinationCityId, string? DestinationDistrictId,
        string? DestinationDistrictName, string DeliveryAddress, long CodAmount, string Description, bool OpenOnDelivery, bool Fragile, string Note, int Quantity, long Fee);

    public sealed record Validation(IReadOnlyList<ParcelImportRowDto> Items, IReadOnlyList<ReadyRow> Ready);

    public static Dictionary<string, string> EmptyValues() => ImportColumns.Fields.ToDictionary(f => f, _ => string.Empty);

    /// <summary>`context` : le vendeur, son adresse de ramassage, son agence.</summary>
    public static async Task<Context> ContextAsync(IAppDbContext db, ICurrentUser caller, string? requested, CancellationToken cancellationToken)
    {
        if (caller.ClientId is not null && !string.IsNullOrEmpty(requested) && requested != caller.ClientId) throw BusinessException.Forbidden("Import impossible pour un autre e-commerçant.");
        var clientId = caller.ClientId ?? (string.IsNullOrEmpty(requested) ? null : requested)
                       ?? throw BusinessException.Validation("Choisissez l’e-commerçant de l’import.", new { field = "clientId" });
        var client = await db.Clients.AsNoTracking().Where(c => c.Id == clientId && c.DeletedAt == null)
            .Select(c => new
            {
                c.Id, c.Name, c.IsPro, c.CityId, c.Address, CityName = c.City.Name,
                Pickup = c.PickupAddresses.Where(a => a.DeletedAt == null).OrderByDescending(a => a.IsDefault).ThenBy(a => a.CreatedAt)
                    .Select(a => new { a.Address, a.CityId, CityName = a.City.Name }).FirstOrDefault(),
            }).FirstOrDefaultAsync(cancellationToken)
            ?? throw BusinessException.NotFound("E-commerçant introuvable.");
        var originCityId = client.Pickup?.CityId ?? client.CityId;
        var originCityName = client.Pickup?.CityName ?? client.CityName;
        var pickupAddress = (client.Pickup?.Address ?? client.Address ?? string.Empty).Trim();
        if (pickupAddress.Length < 1)
        {
            throw BusinessException.Validation($"« {client.Name} » n’a pas d’adresse de ramassage. Renseignez-la sur sa fiche avant d’importer.",
                new { field = "clientId", reason = "missing-pickup-address" });
        }
        var agencyId = await db.Agencies.AsNoTracking().Where(a => a.CityId == originCityId && a.DeletedAt == null).OrderBy(a => a.CreatedAt).Select(a => a.Id).FirstOrDefaultAsync(cancellationToken)
                       ?? await db.Agencies.AsNoTracking().Where(a => a.DeletedAt == null).OrderBy(a => a.CreatedAt).Select(a => a.Id).FirstOrDefaultAsync(cancellationToken)
                       ?? throw BusinessException.Validation("Aucune agence disponible.");
        return new Context(client.Id, client.Name, originCityId, originCityName, pickupAddress, agencyId, client.IsPro);
    }

    /// <summary>`readRows` : les lignes saisies, ou le classeur (colonnes reconnues par en-tête ou alias).</summary>
    public static List<ReadRow> ReadRows(IExcelService excel, string? content, IReadOnlyList<ParcelImportRowInput>? rows)
    {
        if (rows is not null)
        {
            if (rows.Count > MaxRows) throw TooManyRows(rows.Count);
            return rows.Select((r, index) =>
            {
                var values = EmptyValues();
                foreach (var (key, value) in r.Cells()) values[key] = value;
                return new ReadRow(r.Row is { } row ? (int)row : index + 2, values);
            }).ToList();
        }
        var table = SheetReader.Read(excel, content ?? string.Empty);
        var columns = ResolveColumns(table.RawHeader);
        if (table.Rows.Count > MaxRows) throw TooManyRows(table.Rows.Count);
        var read = new List<ReadRow>();
        for (var position = 0; position < table.Rows.Count; position++)
        {
            var cells = table.Rows[position];
            var row = position < table.RowNumbers.Count ? table.RowNumbers[position] : position + 2;
            if (read.Count > 0 && row > read[^1].Row + 1) read.Add(new ReadRow(read[^1].Row + 1, EmptyValues()));
            var values = EmptyValues();
            foreach (var field in ImportColumns.Fields)
            {
                var index = columns[field];
                if (index >= 0) values[field] = (index < cells.Count ? cells[index] : string.Empty).Trim();
            }
            read.Add(new ReadRow(row, values));
        }
        return read;
    }

    private static BusinessException TooManyRows(int count) =>
        BusinessException.Validation($"Ce fichier contient {count} lignes ; la limite est de {MaxRows}. Découpez-le en plusieurs classeurs.", new { field = "content", limit = MaxRows, rows = count });

    private static Dictionary<string, int> ResolveColumns(IReadOnlyList<string> rawHeader)
    {
        var header = rawHeader.Select(ImportColumns.NormalizeLabel).ToList();
        var resolved = new Dictionary<string, int>();
        foreach (var field in ImportColumns.Fields)
        {
            var names = ImportColumns.Aliases[field].Select(ImportColumns.NormalizeLabel).ToHashSet();
            foreach (var labels in ImportColumns.Headers.Values) names.Add(ImportColumns.NormalizeLabel(labels[field]));
            resolved[field] = header.FindIndex(name => name.Length > 0 && names.Contains(name));
        }
        var missing = ImportColumns.Required.Where(f => resolved[f] < 0).ToList();
        if (missing.Count > 0) throw BusinessException.Validation("Colonnes obligatoires absentes. Partez du modèle téléchargeable.", new { field = "content", missing, found = rawHeader });
        return resolved;
    }

    /// <summary>`validate` : erreurs bloquantes, avertissements, frais estimé de chaque ligne.</summary>
    public static async Task<Validation> ValidateAsync(IAppDbContext db, Context context, IReadOnlyList<ReadRow> rows, CancellationToken cancellationToken)
    {
        var cities = new Dictionary<string, string>();
        foreach (var city in await db.Cities.AsNoTracking().Where(c => c.DeletedAt == null).Select(c => new { c.Id, c.Name, c.Code }).ToListAsync(cancellationToken))
        {
            cities[ImportColumns.NormalizeLabel(city.Code)] = city.Id;
            cities[ImportColumns.NormalizeLabel(city.Name)] = city.Id;
        }
        var districts = (await db.Districts.AsNoTracking().Where(d => d.DeletedAt == null).Select(d => new { d.Id, d.Name, d.CityId, d.IsActive }).ToListAsync(cancellationToken))
            .GroupBy(d => d.CityId).ToDictionary(g => g.Key, g => g.ToList());
        var known = (await db.Parcels.AsNoTracking().Where(p => p.ClientId == context.ClientId && p.DeletedAt == null && p.ExternalCode != null)
            .Select(p => p.ExternalCode!).ToListAsync(cancellationToken)).Select(c => c.ToUpperInvariant()).ToHashSet();
        var seen = new Dictionary<string, int>();
        var fees = new Dictionary<string, long>();
        var items = new List<ParcelImportRowDto>();
        var ready = new List<ReadyRow>();
        foreach (var (row, values) in rows)
        {
            var errors = new List<ImportRowError>();
            var warnings = new List<ImportRowError>();
            void Push(string column, string code, string message) => errors.Add(new ImportRowError(row, column, code, message));
            void Warn(string column, string code, string message) => warnings.Add(new ImportRowError(row, column, code, message));
            if (ImportColumns.Fields.All(f => values.GetValueOrDefault(f, string.Empty).Trim().Length == 0))
            {
                items.Add(new ParcelImportRowDto(row, values, [new ImportRowError(row, null, "empty-row", "Ligne vide au milieu du fichier : remplissez-la, ou supprimez-la du classeur.")]) { Fee = null });
                continue;
            }
            var externalCode = values["externalCode"].Trim();
            if (externalCode.Length > 0)
            {
                var key = externalCode.ToUpperInvariant();
                if (seen.TryGetValue(key, out var twin)) Push("externalCode", "duplicate-in-file", $"Référence déjà utilisée ligne {twin}.");
                else
                {
                    if (known.Contains(key)) Push("externalCode", "duplicate-external-code", $"Référence déjà utilisée : un colis de ce vendeur porte déjà « {externalCode} ».");
                    seen[key] = row;
                }
            }
            var recipientName = values["recipientName"].Trim();
            if (recipientName.Length < 1) Push("recipientName", "required", "Nom du destinataire obligatoire.");
            else if (recipientName.Any(char.IsAsciiDigit)) Warn("recipientName", "recipient-has-digits", "Le nom du destinataire contient des chiffres : vérifiez-le.");
            var phones = ParcelInputs.SplitPhones(values["recipientPhone"]);
            var phone = string.Join(ParcelInputs.RecipientPhonesJoin, phones);
            if (phones.Count == 0 || phones.Any(p => !Phones.Pattern.IsMatch(p))) Push("recipientPhone", "invalid-phone", $"« {values["recipientPhone"]} » : chiffres et + uniquement (pas de lettre).");
            else if (phones.Count > ParcelInputs.RecipientPhonesMax) Push("recipientPhone", "too-many-phones", $"« {values["recipientPhone"]} » : {ParcelInputs.RecipientPhonesMax} numéros au plus dans la cellule.");
            else if (phones.Any(p => p.Count(char.IsAsciiDigit) < PhoneDigitsMin)) Warn("recipientPhone", "phone-short", $"Téléphone de moins de {PhoneDigitsMin} chiffres : vérifiez-le.");
            var cityId = cities.GetValueOrDefault(ImportColumns.NormalizeLabel(values["city"]));
            if (values["city"].Trim().Length == 0) Push("city", "required", "Ville absente.");
            else if (cityId is null) Push("city", "unknown-city", $"Ville inconnue : « {values["city"]} ».");
            (string? Id, string? Name) district = (null, null);
            var typedDistrict = values["district"].Trim();
            if (typedDistrict.Length > 0)
            {
                var key = CityNames.DistrictKey(typedDistrict);
                var candidates = (cityId is not null ? districts.GetValueOrDefault(cityId) : null)?.Where(d => CityNames.DistrictKey(d.Name) == key).ToList() ?? [];
                var match = candidates.FirstOrDefault(d => d.IsActive) ?? candidates.FirstOrDefault();
                district = match is null ? (null, typedDistrict) : (match.Id, match.Name);
            }
            if (cityId is not null && district.Name is not null && district.Id is null) Warn("district", "district-unknown", "Quartier inconnu pour cette ville : il sera gardé tel quel.");
            var address = values["address"].Trim();
            if (address.Length < 1) Push("address", "required", "Adresse obligatoire.");
            else if (address.Length < AddressShort || !LetterOrDigit.IsMatch(address)) Warn("address", "address-short", "Adresse très courte : vérifiez-la.");
            var cod = ImportColumns.ReadAmount(values["cod"]);
            if (cod is null) Push("cod", "not-a-number", $"« {values["cod"]} » n’est pas un montant.");
            else if (cod < 0) Push("cod", "negative-amount", "Un montant COD ne peut pas être négatif.");
            else if (cod > 100_000_000) Push("cod", "amount-too-large", "Montant COD hors limites.");
            else if (cod == 0) Warn("cod", "cod-zero", "COD à 0 : rien ne sera encaissé à la livraison.");
            var description = values["description"].Trim();
            if (description.Length < 1) Push("description", "required", "Description obligatoire.");
            foreach (var field in new[] { "openOnDelivery", "fragile" })
            {
                if (values[field].Trim().Length > 0 && ImportColumns.ReadBoolean(values[field], null) is null) Push(field, "not-a-boolean", $"« {values[field]} » : répondez O ou N.");
            }
            var rawQuantity = values["quantity"].Trim();
            var quantity = 1;
            if (rawQuantity.Length > 0)
            {
                if (!Digits.IsMatch(rawQuantity) || !int.TryParse(rawQuantity, out var parsed) || parsed < 1 || parsed > ParcelInputs.QuantityMax)
                {
                    Push("quantity", "not-an-integer", $"« {values["quantity"]} » : un nombre entier de cartons (1, 2, 3…).");
                }
                else quantity = parsed;
            }
            long? fee = null;
            if (errors.Count == 0 && cityId is not null)
            {
                if (!fees.TryGetValue(cityId, out var unit))
                {
                    unit = (await ParcelPricing.QuoteAsync(db, context.OriginCityId, cityId, "district", context.ClientId, cancellationToken)).Fee;
                    fees[cityId] = unit;
                }
                fee = unit * quantity;
            }
            items.Add(new ParcelImportRowDto(row, values, errors) { Warnings = warnings.Count > 0 ? warnings : null, Fee = fee });
            if (errors.Count == 0 && cityId is not null)
            {
                var note = values["notes"].Trim();
                ready.Add(new ReadyRow(row, externalCode.Length == 0 ? null : externalCode, recipientName, phone, cityId, district.Id, district.Name, address, cod ?? 0, description,
                    ImportColumns.ReadBoolean(values["openOnDelivery"], false) ?? false, ImportColumns.ReadBoolean(values["fragile"], false) ?? false,
                    note.Length > 1000 ? note[..1000] : note, quantity, fee ?? 0));
            }
        }
        return new Validation(items, ready);
    }

    /// <summary>`write` : numéros réservés par destination, colis, premier événement, note.</summary>
    public static async Task<List<string>> WriteAsync(IAppDbContext db, ICurrentUser caller, Context context, string importId, IReadOnlyList<ReadyRow> rows, string author, DateTime now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var pending = new Dictionary<string, Queue<string>>();
        foreach (var destination in rows.Select(r => r.DestinationCityId).Distinct())
        {
            var count = rows.Count(r => r.DestinationCityId == destination);
            pending[destination] = new Queue<string>(await ParcelCodes.ReserveManyAsync(db, context.OriginCityId, destination, now, count, cancellationToken));
        }
        var created = new List<string>();
        foreach (var row in rows)
        {
            if (!pending.TryGetValue(row.DestinationCityId, out var codes) || !codes.TryDequeue(out var code)) throw BusinessException.Conflict("Numéro de colis indisponible.");
            var parcel = new Parcel
            {
                Code = code,
                ExternalCode = row.ExternalCode,
                ClientId = context.ClientId,
                AgencyId = context.AgencyId,
                ImportId = importId,
                StatusId = 1,
                StatusAt = now,
                OriginCityId = context.OriginCityId,
                DestinationCityId = row.DestinationCityId,
                DestinationDistrictId = row.DestinationDistrictId,
                DestinationDistrictName = row.DestinationDistrictName,
                DeliveryMode = DeliveryMode.DISTRICT,
                CurrentCityId = context.OriginCityId,
                Description = row.Description,
                RecipientName = row.RecipientName,
                RecipientPhone = row.RecipientPhone,
                PickupAddress = context.PickupAddress,
                DeliveryAddress = row.DeliveryAddress,
                CodAmount = Money.FromCents(row.CodAmount),
                DeliveryFee = Money.FromCents(row.Fee),
                Quantity = row.Quantity,
                FeeIncluded = true,
                OpenOnDelivery = row.OpenOnDelivery,
                Fragile = row.Fragile,
                ExpectedPayment = context.IsPro ? ExpectedPayment.CASH : null,
            };
            db.Parcels.Add(parcel);
            db.ParcelEvents.Add(new ParcelEvent { ParcelId = parcel.Id, StatusId = 1, Comment = $"Import Excel, ligne {row.Row}", AuthorId = caller.UserId, AuthorName = author, CityId = context.OriginCityId, CreatedAt = now });
            if (row.Note.Length > 0) db.ParcelNotes.Add(new ParcelNote { ParcelId = parcel.Id, Body = row.Note, AuthorId = caller.UserId, AuthorName = author });
            created.Add(code);
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return created;
    }
}
