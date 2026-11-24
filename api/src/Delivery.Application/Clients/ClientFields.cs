using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;

namespace Delivery.Application.Clients;

/// <summary>`clientSchema` et `clientPatchSchema` lus sur le corps brut.</summary>
public static class ClientFields
{
    public static readonly string[] Types = ["individual", "merchant", "company"];
    public static readonly string[] BillingTypes = ["normal", "professional"];
    public static readonly Regex TaxId = new(@"^\d{15}$", RegexOptions.Compiled);

    /// <summary>Champs reconnus (transformés) ; `partial` : rien n'est requis.</summary>
    public static JsonObject Parse(JsonNode? root, bool partial, List<(string Path, string Message)> issues)
    {
        var body = new JsonBody(root);
        var output = new JsonObject();
        if (!body.IsObject)
        {
            issues.Add((string.Empty, Zod.Expected("object", root is null ? "undefined" : JsonBody.Kind(root))));
            return output;
        }
        void Required(string key, string type = "string")
        {
            if (!partial && !body.Has(key)) issues.Add((key, Zod.Expected(type)));
        }

        Required("name");
        if (body.Text("name", 2, 80, "too-short") is { } name) output["name"] = name;
        if (body.Has("legalName"))
        {
            var legal = body.TextOrEmpty("legalName", v => v.Trim().Length <= 120, nullable: partial);
            output["legalName"] = legal?.Trim();
        }
        if (body.Option("type", Types) is { } type) output["type"] = type;
        if (body.Has("taxId"))
        {
            var tax = body.TextOrEmpty("taxId", v => TaxId.IsMatch(v.Trim()), nullable: partial);
            output["taxId"] = tax?.Trim();
        }
        Required("phone");
        if (body.Has("phone"))
        {
            var raw = body.Text("phone", trim: false);
            if (raw is not null)
            {
                var phone = Phones.Normalize(raw);
                if (!Phones.Pattern.IsMatch(phone)) issues.Add(("phone", "invalid-phone"));
                else output["phone"] = phone;
            }
        }
        Required("email");
        if (body.Has("email"))
        {
            var email = body.Text("email", trim: false);
            if (email is not null && !Zod.IsEmail(email)) issues.Add(("email", "invalid-email"));
            else if (email is not null) output["email"] = email;
        }
        Required("cityId");
        if (body.Text("cityId", 1, null, "required", trim: false) is { } cityId) output["cityId"] = cityId;
        if (body.Has("agencyId"))
        {
            var agency = body.TextOrEmpty("agencyId", v => v.Length >= 1, nullable: partial);
            output["agencyId"] = agency;
        }
        foreach (var (key, max) in new[] { ("address", 255), ("notes", 1000) })
        {
            if (!body.Has(key)) continue;
            output[key] = body.TextOrEmpty(key, v => v.Trim().Length <= max, nullable: partial)?.Trim();
        }
        if (body.Option("billingType", BillingTypes) is { } billing) output["billingType"] = billing;
        foreach (var key in new[] { "monthlyBilling", "isPro", "isActive" })
        {
            if (body.Bool(key) is { } flag) output[key] = flag;
        }
        issues.AddRange(body.Issues);
        return output;
    }

    /// <summary>Écrit les champs présents du patch sur la fiche (`'champ' in input`).</summary>
    public static void Apply(Client client, JsonObject input)
    {
        string? Text(string key) => input[key]?.GetValue<string>();
        if (Text("name") is { } name) client.Name = name;
        if (Text("phone") is { } phone) client.Phone = phone;
        if (Text("email") is { } email) client.Email = email;
        if (Text("type") is { } type) client.Type = Enum.Parse<ClientType>(Money.FromWire(type));
        if (Text("billingType") is { } billing) client.BillingType = Enum.Parse<BillingType>(Money.FromWire(billing));
        if (input["isPro"] is JsonValue isPro) client.IsPro = isPro.GetValue<bool>();
        if (input["isActive"] is JsonValue isActive) client.IsActive = isActive.GetValue<bool>();
        if (input["monthlyBilling"] is JsonValue monthly) client.MonthlyBilling = monthly.GetValue<bool>();
        if (Text("cityId") is { } cityId) client.CityId = cityId;
        if (input.ContainsKey("legalName")) client.LegalName = Zod.EmptyToNull(Text("legalName"));
        if (input.ContainsKey("taxId")) client.TaxId = Zod.EmptyToNull(Text("taxId"));
        if (input.ContainsKey("address")) client.Address = Zod.EmptyToNull(Text("address"));
        if (input.ContainsKey("notes")) client.Notes = Zod.EmptyToNull(Text("notes"));
        if (input.ContainsKey("agencyId")) client.AgencyId = Zod.EmptyToNull(Text("agencyId"));
    }
}
