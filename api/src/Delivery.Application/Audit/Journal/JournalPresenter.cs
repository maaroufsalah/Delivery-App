using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Delivery.Domain.Parcels;

namespace Delivery.Application.Audit.Journal;

/// <summary>`JournalChange`. Une valeur est un objet `{ kind, … }` ou `null`.</summary>
public sealed record JournalChangeDto(string Field, IReadOnlyDictionary<string, object>? Before, IReadOnlyDictionary<string, object>? After);

public sealed record JournalObjectDto(string Type, string? Reference, string? Href);

public sealed record JournalUserDto(string Name, string Role, string? Email);

/// <summary>`JournalEntry`.</summary>
public class JournalEntryDto
{
    public string Id { get; set; } = string.Empty;
    public DateTime At { get; set; }
    public string Action { get; set; } = string.Empty;
    public JournalChangeDto? Headline { get; set; }
    public JournalObjectDto Object { get; set; } = new("other", null, null);
    public int Count { get; set; }
    public JournalUserDto? User { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? Agency { get; set; }
    public string Category { get; set; } = string.Empty;
}

/// <summary>`JournalEntryDetail`.</summary>
public sealed class JournalEntryDetailDto : JournalEntryDto
{
    [JsonPropertyOrder(1)]
    public List<JournalChangeDto> Changes { get; set; } = [];

    [JsonPropertyOrder(1)]
    public string? Reason { get; set; }

    [JsonPropertyOrder(1)]
    public string? Ip { get; set; }

    [JsonPropertyOrder(1)]
    public List<JournalObjectDto> Related { get; set; } = [];

    public JournalEntryDto Summary() => new()
    {
        Id = Id, At = At, Action = Action, Headline = Headline, Object = Object, Count = Count, User = User, Source = Source, Agency = Agency, Category = Category,
    };
}

/// <summary>Une ligne d'audit lue pour le journal (avant et après en JSON).</summary>
public sealed record JournalRow(string Id, string Entity, string EntityId, string Action, JsonElement Before, JsonElement After, IReadOnlyList<string> Diff,
    string? UserId, string? UserEmail, string? Role, string? Ip, string? UserAgent, string? RequestId, string? Source, DateTime At);

/// <summary>`JournalLookups` : les noms des identifiants cités.</summary>
public sealed record JournalLookups(
    IReadOnlyDictionary<string, string> Cities,
    IReadOnlyDictionary<string, string> Agencies,
    IReadOnlyDictionary<string, string> Couriers,
    IReadOnlyDictionary<string, string> Clients,
    IReadOnlyDictionary<string, (string Name, string? Email)> Users,
    IReadOnlyDictionary<string, string> Parcels,
    IReadOnlyDictionary<string, string> Reasons);

/// <summary>`journal.presenter.ts` : une requête (`requestId`) devient une entrée lisible.</summary>
public static class JournalPresenter
{
    private static readonly Regex MobileAgent = new("okhttp|expo|dalvik|cfnetwork|darwin", RegexOptions.Compiled);

    private static readonly Dictionary<string, Dictionary<string, string>> StatusActions = new()
    {
        ["DeliveryNote"] = new() { ["VALIDATED"] = "validate" },
        ["Invoice"] = new() { ["ISSUED"] = "issue", ["PAID"] = "pay" },
        ["CodPayout"] = new() { ["PAID"] = "pay" },
        ["CashRemittance"] = new() { ["CONFIRMED"] = "close", ["CANCELLED"] = "cancel" },
        ["Route"] = new() { ["CLOSED"] = "close" },
        ["ReturnHandover"] = new() { ["CLOSED"] = "close", ["CANCELLED"] = "cancel" },
        ["Order"] = new() { ["CANCELLED"] = "cancel" },
        ["Claim"] = new() { ["RESOLVED"] = "close" },
        ["PettyCashMovement"] = new() { ["VALIDATED"] = "validate" },
        ["PettyCashDay"] = new() { ["CLOSED"] = "close" },
    };

    public static bool IsTechnicalOnly(JournalRow row) => row.Action == "UPDATE" && row.Diff.All(f => JournalCatalog.Technical.Contains(f));

    public static bool IsBusinessRow(JournalRow row) =>
        row.Action is "LOGIN" or "LOGIN_FAILED" || (!JournalCatalog.Companions.Contains(row.Entity) && !IsTechnicalOnly(row));

    private static JsonElement Get(JsonElement data, string key) =>
        data.ValueKind == JsonValueKind.Object && data.TryGetProperty(key, out var value) ? value : default;

    /// <summary>`dataOf` : avant puis après fusionnés (l'après l'emporte).</summary>
    private static JsonElement DataOf(JournalRow row)
    {
        var merged = new Dictionary<string, JsonElement>();
        foreach (var source in new[] { row.Before, row.After })
        {
            if (source.ValueKind != JsonValueKind.Object) continue;
            foreach (var p in source.EnumerateObject()) merged[p.Name] = p.Value;
        }
        return JsonSerializer.SerializeToElement(merged);
    }

    private static IReadOnlyDictionary<string, object> Value(string kind, string key, object value) => new Dictionary<string, object> { ["kind"] = kind, [key] = value };

    /// <summary>`presentValue`.</summary>
    public static IReadOnlyDictionary<string, object>? Present(string kind, JsonElement raw, JournalLookups lookups)
    {
        if (kind == "secret") return raw.ValueKind == JsonValueKind.Undefined ? null : Value("enum", "value", "changed");
        if (raw.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null || (raw.ValueKind == JsonValueKind.String && raw.GetString() == string.Empty)) return null;
        string Str() => raw.ValueKind == JsonValueKind.String ? raw.GetString()! : raw.GetRawText();
        IReadOnlyDictionary<string, object>? Named(string? name) => name is null ? null : Value("text", "text", name);
        switch (kind)
        {
            case "money":
                double number = raw.ValueKind == JsonValueKind.Number ? raw.GetDouble()
                    : raw.ValueKind == JsonValueKind.String && double.TryParse(raw.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : double.NaN;
                return double.IsFinite(number) ? Value("money", "cents", (long)Math.Floor(number * 100 + 0.5)) : null;
            case "date":
                return DateTime.TryParse(Str(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date)
                    ? Value("date", "iso", DateTime.SpecifyKind(date, DateTimeKind.Utc)) : null;
            case "number":
                return raw.ValueKind == JsonValueKind.Number ? Value("number", "value", raw.GetDouble()) : null;
            case "bool":
                return raw.ValueKind is JsonValueKind.True or JsonValueKind.False ? Value("bool", "value", raw.GetBoolean()) : null;
            case "enum":
                return Value("enum", "value", Str().ToLowerInvariant().Replace('_', '-'));
            case "parcel-status":
                var id = raw.ValueKind == JsonValueKind.Number ? raw.GetDouble() : double.TryParse(Str(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : double.NaN;
                return double.IsFinite(id) && StatusCatalog.Find((int)id) is { } status && status.Id == id ? Value("parcel-status", "key", status.Key) : null;
            case "ref:city": return Named(lookups.Cities.GetValueOrDefault(Str()));
            case "ref:agency": return Named(lookups.Agencies.GetValueOrDefault(Str()));
            case "ref:courier": return Named(lookups.Couriers.GetValueOrDefault(Str()));
            case "ref:client": return Named(lookups.Clients.GetValueOrDefault(Str()));
            case "ref:user": return Named(lookups.Users.TryGetValue(Str(), out var user) ? user.Name : null);
            default:
                var text = Str();
                return Value("text", "text", text.Length > 160 ? string.Concat(text.AsSpan(0, 157), "…") : text);
        }
    }

    private static List<JournalChangeDto> ChangesOf(JournalRow row, JournalEntityConfig config, JournalLookups lookups)
    {
        IEnumerable<string> fields = row.Action == "UPDATE" ? row.Diff.Where(config.Fields.ContainsKey)
            : row.Action == "CREATE" ? config.Fields.Keys.Where(f => Get(row.After, f) is var v && v.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null) && !(v.ValueKind == JsonValueKind.String && v.GetString() == string.Empty))
            : [];
        return fields.Select(field =>
        {
            var kind = config.Fields[field];
            return new JournalChangeDto(field == "passwordHash" ? "password" : field,
                row.Action == "CREATE" ? null : OpenRemittance(row, field, Present(kind, Get(row.Before, field), lookups)),
                OpenRemittance(row, field, Present(kind, Get(row.After, field), lookups)));
        }).ToList();
    }

    private static IReadOnlyDictionary<string, object>? OpenRemittance(JournalRow row, string field, IReadOnlyDictionary<string, object>? value) =>
        row.Entity == "CashRemittance" && field == "status" && value is not null && (string)value["kind"] == "enum" && (string)value["value"] == "draft" ? Value("enum", "value", "open") : value;

    /// <summary>`actionOf` : l'action lisible et la ligne qui la résume.</summary>
    public static (string Action, JournalChangeDto? Headline) ActionOf(JournalRow row, JournalEntityConfig config, IReadOnlyList<JournalChangeDto> changes)
    {
        if (row.Action == "LOGIN") return ("login", null);
        if (row.Action == "LOGIN_FAILED") return ("login-failed", null);
        if (row.Action is "CREATE" or "CREATE_MANY") return ("create", null);
        if (row.Action is "DELETE" or "DELETE_MANY") return ("delete", null);
        var deletedAt = Get(row.After, "deletedAt");
        if (row.Diff.Contains("deletedAt") && Truthy(deletedAt)) return ("delete", null);
        if (row.Entity == "Parcel" && row.Diff.Contains("clientId")) return ("client-change", changes.FirstOrDefault(c => c.Field == "clientId"));
        var statusField = row.Diff.Contains("statusId") ? "statusId" : row.Diff.Contains("status") ? "status" : null;
        if (statusField is not null)
        {
            var headline = changes.FirstOrDefault(c => c.Field == statusField);
            var value = Get(row.After, statusField);
            var key = value.ValueKind == JsonValueKind.String ? value.GetString()! : value.ValueKind == JsonValueKind.Undefined ? "undefined" : value.GetRawText();
            var named = StatusActions.TryGetValue(row.Entity, out var actions) && actions.TryGetValue(key, out var action) ? action : null;
            return (named ?? "status", headline);
        }
        var money = changes.FirstOrDefault(c => config.Fields.GetValueOrDefault(c.Field) == "money");
        return money is not null ? ("amount", money) : ("update", null);
    }

    private static bool Truthy(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString()!.Length > 0,
        JsonValueKind.True => true,
        JsonValueKind.Number => value.GetDouble() != 0,
        JsonValueKind.Object or JsonValueKind.Array => true,
        _ => false,
    };

    public static string RoleOf(string? role) => role switch
    {
        "SUPER_ADMIN" => "admin",
        "STAFF" => "staff",
        "MERCHANT" => "merchant",
        "COURIER" => "courier",
        "PLATFORM_ADMIN" => "platform",
        _ => "other",
    };

    public static string SourceOf(JournalRow row)
    {
        if (row.Source is "web" or "mobile" or "import") return row.Source;
        if (row.UserId is null && row.UserEmail is null && row.Ip is null) return "auto";
        return MobileAgent.IsMatch((row.UserAgent ?? string.Empty).ToLowerInvariant()) ? "mobile" : "web";
    }

    private static string? StringOf(JsonElement data, string key) => Get(data, key) is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;

    private static JournalObjectDto ObjectOf(JournalRow row, JournalLookups lookups)
    {
        var config = JournalCatalog.Of(row.Entity);
        var data = DataOf(row);
        var reference = JournalCatalog.ReferenceOf(row.Entity, data);
        if (row.Entity == "PricingRule")
        {
            var origin = lookups.Cities.GetValueOrDefault(StringOf(data, "originCityId") ?? string.Empty);
            var destination = lookups.Cities.GetValueOrDefault(StringOf(data, "destinationCityId") ?? string.Empty);
            reference = origin is not null && destination is not null ? $"{origin} → {destination}" : null;
        }
        if (row.Action is "LOGIN" or "LOGIN_FAILED") reference = row.EntityId.Length > 0 && lookups.Users.TryGetValue(row.EntityId, out var user) ? user.Name : null;
        return new JournalObjectDto(JournalCatalog.TypeOf(config, data), reference, reference is not null ? JournalCatalog.HrefOf(row.Entity, data, row.EntityId) : null);
    }

    /// <summary>`presentGroup`.</summary>
    public static JournalEntryDetailDto? PresentGroup(string groupId, IReadOnlyList<JournalRow> rows, JournalLookups lookups)
    {
        var business = rows.Where(IsBusinessRow).ToList();
        if (business.Count == 0) return null;
        var main = business.OrderByDescending(r => JournalCatalog.Of(r.Entity).Priority).ThenBy(r => r.At).First();
        var config = JournalCatalog.Of(main.Entity);
        var changes = ChangesOf(main, config, lookups);
        var (action, headline) = ActionOf(main, config, changes);
        var obj = ObjectOf(main, lookups);
        var data = DataOf(main);
        var sameType = business.Where(r => r.Entity == main.Entity).ToList();
        var countValue = Get(main.After, "count");
        var count = main.Action.EndsWith("_MANY", StringComparison.Ordinal) && countValue.ValueKind == JsonValueKind.Number ? (int)countValue.GetDouble() : sameType.Select(r => r.EntityId).Distinct().Count();
        var seen = new HashSet<string> { $"{obj.Type}|{obj.Reference}" };
        var related = new List<JournalObjectDto>();
        foreach (var row in business)
        {
            if (ReferenceEquals(row, main) || row.Entity == main.Entity) continue;
            var other = ObjectOf(row, lookups);
            var key = $"{other.Type}|{other.Reference}";
            if (other.Reference is not null && seen.Add(key) && related.Count < 20) related.Add(other);
        }
        JournalUserDto? author = main.Action == "LOGIN_FAILED" || (main.UserId is null && main.UserEmail is null) ? null
            : new JournalUserDto((main.UserId is not null && lookups.Users.TryGetValue(main.UserId, out var u) ? u.Name : null) ?? main.UserEmail ?? string.Empty, RoleOf(main.Role), main.UserEmail);
        return new JournalEntryDetailDto
        {
            Id = groupId,
            At = main.At,
            Action = action,
            Headline = headline,
            Object = obj,
            Count = Math.Max(count, 1),
            User = author,
            Source = SourceOf(main),
            Agency = lookups.Agencies.GetValueOrDefault(StringOf(data, "agencyId") ?? string.Empty),
            Category = config.Category,
            Changes = changes,
            Reason = ReasonOf(main, rows, lookups),
            Ip = main.Ip,
            Related = related,
        };
    }

    private static string? ReasonOf(JournalRow main, IReadOnlyList<JournalRow> rows, JournalLookups lookups)
    {
        if (lookups.Reasons.TryGetValue(main.EntityId, out var saved)) return saved;
        if (StringOf(main.After, "varianceReason") is { } variance && variance.Trim().Length > 0) return variance.Trim();
        foreach (var row in rows)
        {
            if (row.Entity != "ParcelEvent") continue;
            if (StringOf(row.After, "comment") is { } comment && comment.Trim().Length > 0) return comment.Trim();
        }
        return null;
    }

    /// <summary>`idsToResolve`.</summary>
    public static Dictionary<string, HashSet<string>> IdsToResolve(IReadOnlyList<JournalRow> rows)
    {
        var ids = new[] { "cities", "agencies", "couriers", "clients", "users", "parcels" }.ToDictionary(k => k, _ => new HashSet<string>());
        foreach (var row in rows)
        {
            if (row.UserId is not null) ids["users"].Add(row.UserId);
            if (row.Action is "LOGIN" or "LOGIN_FAILED" && row.EntityId.Length > 0) ids["users"].Add(row.EntityId);
            var config = JournalCatalog.Of(row.Entity);
            foreach (var data in new[] { row.Before, row.After })
            {
                foreach (var (field, kind) in config.Fields)
                {
                    if (StringOf(data, field) is not { Length: > 0 } value) continue;
                    var bucket = kind switch { "ref:city" => "cities", "ref:agency" => "agencies", "ref:courier" => "couriers", "ref:client" => "clients", "ref:user" => "users", _ => null };
                    if (bucket is not null) ids[bucket].Add(value);
                }
                if (StringOf(data, "agencyId") is { } agencyId) ids["agencies"].Add(agencyId);
                if (row.Entity == "PricingRule")
                {
                    if (StringOf(data, "originCityId") is { } origin) ids["cities"].Add(origin);
                    if (StringOf(data, "destinationCityId") is { } destination) ids["cities"].Add(destination);
                }
                if (row.Entity == "ParcelEvent" && StringOf(data, "parcelId") is { } parcelId) ids["parcels"].Add(parcelId);
            }
        }
        return ids;
    }

    /// <summary>`valueText` (export).</summary>
    public static string ValueText(IReadOnlyDictionary<string, object>? value)
    {
        if (value is null) return "—";
        return (string)value["kind"] switch
        {
            "money" => $"{((long)value["cents"] / 100m).ToString("N2", CultureInfo.GetCultureInfo("fr-FR"))} MAD",
            "date" => Common.Export.Exports.DateTimeText((DateTime)value["iso"]),
            "number" => ((double)value["value"]).ToString(CultureInfo.InvariantCulture),
            "bool" => (bool)value["value"] ? "Oui" : "Non",
            "parcel-status" => StatusCatalog.All.FirstOrDefault(s => s.Key == (string)value["key"]) is { } status ? Common.Documents.DocumentLabels.ParcelStatus(status.Id) : (string)value["key"],
            "enum" => JournalCatalog.EnumValueLabels.GetValueOrDefault((string)value["value"], (string)value["value"]),
            _ => (string)value["text"],
        };
    }

    /// <summary>`changeText`.</summary>
    public static string ChangeText(JournalChangeDto change)
    {
        var label = JournalCatalog.FieldLabels.GetValueOrDefault(change.Field, change.Field);
        if (change.After is { } after && (string)after["kind"] == "enum" && (string)after["value"] == "changed") return $"{label} : modifié";
        return change.Before is null ? $"{label} : {ValueText(change.After)}" : $"{label} : {ValueText(change.Before)} → {ValueText(change.After)}";
    }

    /// <summary>`actionText`.</summary>
    public static string ActionText(JournalEntryDto entry)
    {
        var action = JournalCatalog.ActionLabels.GetValueOrDefault(entry.Action, entry.Action);
        if (entry.Headline is null) return action;
        if (entry.Action == "amount") return ChangeText(entry.Headline);
        return $"{action} : {ValueText(entry.Headline.Before)} → {ValueText(entry.Headline.After)}";
    }

    /// <summary>`objectText`.</summary>
    public static string ObjectText(JournalEntryDto entry)
    {
        var type = JournalCatalog.ObjectLabels.GetValueOrDefault(entry.Object.Type, entry.Object.Type);
        var reference = entry.Object.Reference is null ? string.Empty : $" {entry.Object.Reference}";
        var more = entry.Count > 1 ? $" (+{entry.Count - 1})" : string.Empty;
        return $"{type}{reference}{more}";
    }
}
