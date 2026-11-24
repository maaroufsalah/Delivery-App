using System.Globalization;
using System.Text.Json;

namespace Delivery.Application.Audit.Journal;

/// <summary>Une entité journalisée : sa nature, sa catégorie, sa priorité, ses champs lisibles.</summary>
public sealed record JournalEntityConfig(string Entity, string? Type, string Category, int Priority, IReadOnlyDictionary<string, string> Fields);

/// <summary>`journal-catalog.ts` et `journal-labels.ts` : les tables, et les fonctions `type`, `reference`, `href`.</summary>
public static partial class JournalCatalog
{
    private sealed record Tables(
        IReadOnlyDictionary<string, JournalEntityConfig> Entities,
        JournalEntityConfig Fallback,
        IReadOnlySet<string> Companions,
        IReadOnlyList<string> Technical,
        IReadOnlyDictionary<string, string> ObjectLabels,
        IReadOnlyDictionary<string, string> ActionLabels,
        IReadOnlyDictionary<string, string> FieldLabels,
        IReadOnlyDictionary<string, string> EnumValueLabels,
        IReadOnlyDictionary<string, string> RoleLabels,
        IReadOnlyDictionary<string, string> SourceLabels);

    private static readonly Lazy<Tables> Data = new(Load);

    public static IReadOnlySet<string> Companions => Data.Value.Companions;
    public static IReadOnlyList<string> Technical => Data.Value.Technical;
    public static IReadOnlyDictionary<string, string> ObjectLabels => Data.Value.ObjectLabels;
    public static IReadOnlyDictionary<string, string> ActionLabels => Data.Value.ActionLabels;
    public static IReadOnlyDictionary<string, string> FieldLabels => Data.Value.FieldLabels;
    public static IReadOnlyDictionary<string, string> EnumValueLabels => Data.Value.EnumValueLabels;
    public static IReadOnlyDictionary<string, string> RoleLabels => Data.Value.RoleLabels;
    public static IReadOnlyDictionary<string, string> SourceLabels => Data.Value.SourceLabels;

    /// <summary>`entityConfig`.</summary>
    public static JournalEntityConfig Of(string entity) => Data.Value.Entities.GetValueOrDefault(entity) ?? Data.Value.Fallback;

    /// <summary>`entitiesOfCategory`.</summary>
    public static IReadOnlyList<string> EntitiesOf(string category) => Data.Value.Entities.Values.Where(e => e.Category == category).Select(e => e.Entity).ToList();

    /// <summary>`MONEY_FIELDS`.</summary>
    public static IReadOnlyList<string> MoneyFields => Data.Value.Entities.Values.SelectMany(e => e.Fields.Where(f => f.Value == "money").Select(f => f.Key)).Distinct().ToList();

    private static string? Text(JsonElement data, string key) =>
        data.ValueKind == JsonValueKind.Object && data.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String && v.GetString()!.Trim().Length > 0 ? v.GetString()!.Trim() : null;

    private static string? FullName(JsonElement data)
    {
        var joined = string.Join(' ', new[] { Text(data, "firstName"), Text(data, "lastName") }.Where(s => s is not null)).Trim();
        return joined.Length > 0 ? joined : Text(data, "email") ?? Text(data, "username");
    }

    /// <summary>`typeOf`.</summary>
    public static string TypeOf(JournalEntityConfig config, JsonElement data) =>
        config.Type ?? (config.Entity == "DeliveryNote" ? (Text(data, "type") == "DROPOFF" ? "dropoff-note" : "pickup-note") : "other");

    /// <summary>`reference` de chaque entité.</summary>
    public static string? ReferenceOf(string entity, JsonElement data) => entity switch
    {
        "Cheque" => Text(data, "number"),
        "Payment" => Text(data, "reference") ?? Text(data, "chequeNumber"),
        "PettyCashMovement" => Text(data, "label"),
        "PettyCashDay" => Text(data, "day") is { } day ? day[..Math.Min(10, day.Length)] : null,
        "User" or "Courier" => FullName(data),
        "Client" or "Agency" or "Product" => Text(data, "name"),
        "ClientPickupAddress" => Text(data, "label") ?? Text(data, "address"),
        "PasswordResetRequest" => Text(data, "email"),
        "PricingRule" => null,
        "Setting" => Text(data, "key"),
        "ParcelImport" => Text(data, "filename"),
        _ when Data.Value.Entities.ContainsKey(entity) => Text(data, "code"),
        _ => Text(data, "code") ?? Text(data, "name") ?? Text(data, "label"),
    };

    /// <summary>`href` de chaque entité (lien vers son écran), `null` sans lien.</summary>
    public static string? HrefOf(string entity, JsonElement data, string id)
    {
        string? ByCode(string path) => Text(data, "code") is { } code ? $"{path}/{Uri.EscapeDataString(code)}" : null;
        string? ById(string path) => id.Length > 0 ? $"{path}/{Uri.EscapeDataString(id)}" : null;
        return entity switch
        {
            "Parcel" => ByCode("/parcels"),
            "DeliveryNote" => ByCode(Text(data, "type") == "DROPOFF" ? "/dropoff-notes" : "/pickup-notes"),
            "Route" => ByCode("/routes"),
            "CashRemittance" => ByCode("/remittances"),
            "Invoice" => ByCode("/invoices"),
            "CodPayout" => ByCode("/cod-payouts"),
            "Cheque" => ById("/cheques"),
            "PettyCashMovement" or "PettyCashDay" => "/petty-cash",
            "User" => ById("/users"),
            "Client" => ById("/clients"),
            "Courier" => ById("/couriers"),
            "PricingRule" => "/pricing",
            "Setting" => "/settings/company",
            "Agency" => "/agencies",
            "Claim" => ByCode("/claims"),
            "ReturnHandover" => ByCode("/returns/handovers"),
            "ReturnSlip" => ByCode("/returns/slips"),
            "Product" => ById("/products"),
            "Order" => ByCode("/orders"),
            "ParcelImport" => "/parcels/import",
            _ => null,
        };
    }

    private static Tables Load()
    {
        using var document = JsonDocument.Parse(Json);
        var root = document.RootElement;
        static IReadOnlyDictionary<string, string> Map(JsonElement element) => element.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
        static JournalEntityConfig Config(string name, JsonElement e) => new(name, e.GetProperty("type").ValueKind == JsonValueKind.String ? e.GetProperty("type").GetString() : null,
            e.GetProperty("category").GetString()!, e.GetProperty("priority").GetInt32(), Map(e.GetProperty("fields")));
        var entities = root.GetProperty("entities").EnumerateObject().ToDictionary(p => p.Name, p => Config(p.Name, p.Value));
        return new Tables(entities, Config("__fallback", root.GetProperty("fallback")),
            root.GetProperty("companions").EnumerateArray().Select(e => e.GetString()!).ToHashSet(),
            root.GetProperty("technical").EnumerateArray().Select(e => e.GetString()!).ToList(),
            Map(root.GetProperty("objectLabels")), Map(root.GetProperty("actionLabels")), Map(root.GetProperty("fieldLabels")),
            Map(root.GetProperty("enumValueLabels")), Map(root.GetProperty("roleLabels")), Map(root.GetProperty("sourceLabels")));
    }

    public static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);
}
