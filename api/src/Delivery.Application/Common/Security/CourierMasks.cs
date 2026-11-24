using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Common.Security;

/// <summary>Ce qui se masque, et par quoi (`CourierMask`).</summary>
public sealed record CourierMask(string Alias, Regex? Pattern, IReadOnlySet<string> Exact, string StaffAlias, Regex? StaffPattern, IReadOnlySet<string> StaffExact,
    IReadOnlySet<string>? Own);

/// <summary>
/// `courier-mask.ts` : un compte qui ne voit pas l'identité des livreurs (le vendeur) lit « Livreur &lt;nom court&gt; »
/// à la place de leur nom, sans téléphone ni courriel ; le personnel devient « Équipe &lt;nom court&gt; ». Appliqué à
/// toute réponse JSON (`CourierMaskInterceptor`).
/// </summary>
public static class CourierMasks
{
    private const int MinNeedle = 5;
    private static readonly HashSet<string> NameKeys = ["courierName"];
    private static readonly HashSet<string> AuthorKeys =
        ["authorName", "createdByName", "issuedByName", "assigneeName", "validatedByName", "confirmedByName", "paidByName", "decidedByName", "rejectedByName", "uploadedByName", "removedByName",
            "deliveredByName"];
    private static readonly HashSet<string> SystemAuthors = ["système"];
    private static readonly HashSet<string> ContactKeys = ["courierPhone", "courierEmail"];
    private static readonly HashSet<string> OwnKeys = ["recipientName", "recipientPhone", "deliveryAddress", "pickupAddress", "description", "externalCode", "clientName", "name", "firstName", "lastName", "email", "phone"];

    private sealed record Identity(string FirstName, string LastName, string? Phone, string? Email);

    public static string CourierAlias(string shortName) => shortName.Trim() is { Length: > 0 } s ? $"Livreur {s}" : "Livreur";

    public static string StaffAlias(string shortName, string? locale)
    {
        var s = shortName.Trim();
        return locale switch
        {
            "en" => s.Length > 0 ? $"{s} team" : "Team",
            "ar" => s.Length > 0 ? $"فريق {s}" : "الفريق",
            _ => s.Length > 0 ? $"Équipe {s}" : "Équipe",
        };
    }

    private static string Normalized(string text) => Regex.Replace(text.Trim(), @"\s+", " ").ToLowerInvariant();

    private static string? Needle(string text)
    {
        var words = text.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words).Length < MinNeedle ? null : string.Join(@"\s+", words.Select(Regex.Escape));
    }

    private static Regex? Pattern(IEnumerable<Identity> identities)
    {
        var needles = new HashSet<string>();
        foreach (var who in identities)
        {
            foreach (var name in new[] { $"{who.FirstName} {who.LastName}", $"{who.LastName} {who.FirstName}" }) if (Needle(name) is { } n) needles.Add(n);
            var digits = new string((who.Phone ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
            if (digits.Length >= 8) needles.Add(Regex.Escape(digits));
            if (who.Email?.Trim() is { Length: >= MinNeedle } email) needles.Add(Regex.Escape(email));
        }
        if (needles.Count == 0) return null;
        var alternatives = string.Join('|', needles.OrderByDescending(n => n.Length));
        return new Regex($@"(?<![\p{{L}}\p{{N}}@.])(?:{alternatives})(?![\p{{L}}\p{{N}}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static HashSet<string> Exact(IEnumerable<Identity> identities)
    {
        var exact = new HashSet<string>();
        foreach (var who in identities)
        {
            foreach (var value in new[] { $"{who.FirstName} {who.LastName}", $"{who.LastName} {who.FirstName}", who.Email ?? string.Empty }) if (Normalized(value).Length > 0) exact.Add(Normalized(value));
            var digits = new string((who.Phone ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
            if (digits.Length > 0) exact.Add(digits);
        }
        return exact;
    }

    /// <summary>`loadCourierMask` : livreurs (fiche et compte), personnel, et les noms du lecteur lui-même.</summary>
    public static async Task<CourierMask> LoadAsync(IAppDbContext db, ICurrentUser reader, CancellationToken cancellationToken)
    {
        var company = await db.Companies.AsNoTracking().OrderBy(c => c.CreatedAt).Select(c => new { c.Name, c.ShortName, c.Email }).FirstOrDefaultAsync(cancellationToken);
        var couriers = await db.Couriers.AsNoTracking()
            .Select(c => new { c.FirstName, c.LastName, c.Phone, User = c.User == null ? null : new { c.User.FirstName, c.User.LastName, c.User.Phone, c.User.Email } })
            .ToListAsync(cancellationToken);
        var staff = await db.Users.AsNoTracking().Where(u => u.Client == null && u.Courier == null && u.Roles.Any(r => r.Role == RoleName.SUPER_ADMIN || r.Role == RoleName.STAFF))
            .Select(u => new { u.FirstName, u.LastName, u.Email }).ToListAsync(cancellationToken);
        var identities = couriers.SelectMany(c => c.User is null
            ? new[] { new Identity(c.FirstName, c.LastName, c.Phone, null) }
            : [new Identity(c.FirstName, c.LastName, c.Phone, null), new Identity(c.User.FirstName, c.User.LastName, c.User.Phone, c.User.Email)]).ToList();
        var publicEmail = company?.Email?.Trim().ToLowerInvariant();
        var staffIdentities = staff.Select(u => new Identity(u.FirstName, u.LastName, null, u.Email?.Trim().ToLowerInvariant() == publicEmail ? null : u.Email)).ToList();
        var shortName = company is null ? string.Empty
            : company.ShortName?.Trim() is { Length: > 0 } s ? s
            : company.Name.Trim().Length > 20 ? company.Name.Trim()[..20] : company.Name.Trim();
        var own = await ReaderNamesAsync(db, reader, cancellationToken);
        return new CourierMask(CourierAlias(shortName), Pattern(identities), Exact(identities), StaffAlias(shortName, reader.PreferredLocale), Pattern(staffIdentities),
            Exact(staffIdentities), own);
    }

    private static async Task<HashSet<string>> ReaderNamesAsync(IAppDbContext db, ICurrentUser reader, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().Where(u => u.Id == reader.UserId).Select(u => new { u.FirstName, u.LastName, u.Email }).FirstOrDefaultAsync(cancellationToken);
        var client = reader.ClientId is null ? null : await db.Clients.AsNoTracking().Where(c => c.Id == reader.ClientId).Select(c => c.Name).FirstOrDefaultAsync(cancellationToken);
        var names = new HashSet<string>();
        foreach (var value in new[] { user is null ? string.Empty : $"{user.FirstName} {user.LastName}", user is null ? string.Empty : $"{user.LastName} {user.FirstName}", user?.Email ?? string.Empty, client ?? string.Empty })
        {
            if (Normalized(value).Length > 0) names.Add(Normalized(value));
        }
        return names;
    }

    /// <summary>`maskText` : nom exact remplacé en entier, sinon chaque occurrence dans le texte.</summary>
    public static string MaskText(string text, CourierMask mask)
    {
        if (mask.Exact.Contains(Normalized(text))) return mask.Alias;
        if (mask.StaffExact.Contains(Normalized(text))) return mask.StaffAlias;
        var output = text;
        if (mask.Pattern is not null) output = mask.Pattern.Replace(output, mask.Alias);
        if (mask.StaffPattern is not null) output = mask.StaffPattern.Replace(output, mask.StaffAlias);
        return output;
    }

    private static string? OwnValue(string value, CourierMask mask, string key)
    {
        if (!mask.StaffExact.Contains(Normalized(value))) return value;
        return key switch
        {
            "name" => mask.StaffAlias,
            "email" => null,
            _ => value,
        };
    }

    private static string AuthorValue(string value, CourierMask mask)
    {
        var masked = MaskText(value, mask);
        if (masked != value || mask.Own is null) return masked;
        var name = Normalized(value);
        if (name.Length == 0 || mask.Own.Contains(name) || SystemAuthors.Contains(name)) return value;
        return mask.StaffAlias;
    }

    /// <summary>`maskCouriers` : parcourt la réponse ; la clé guide le traitement de chaque texte.</summary>
    public static JsonNode? Apply(JsonNode? node, CourierMask mask, string? key = null)
    {
        switch (node)
        {
            case JsonValue value when value.TryGetValue<string>(out var text):
                if (key is not null && NameKeys.Contains(key)) return text.Length == 0 ? JsonValue.Create(text) : JsonValue.Create(mask.Alias);
                if (key is not null && ContactKeys.Contains(key)) return null;
                if (key is not null && OwnKeys.Contains(key)) return OwnValue(text, mask, key) is { } own ? JsonValue.Create(own) : null;
                if (key is not null && AuthorKeys.Contains(key)) return JsonValue.Create(AuthorValue(text, mask));
                return JsonValue.Create(MaskText(text, mask));
            case JsonArray array:
                var items = new JsonArray();
                foreach (var item in array) items.Add(Apply(item?.DeepClone(), mask, key));
                return items;
            case JsonObject obj:
                if (key == "courier" && (obj.ContainsKey("firstName") || obj.ContainsKey("lastName")))
                {
                    var courier = (JsonObject)obj.DeepClone();
                    if (courier.ContainsKey("firstName")) courier["firstName"] = mask.Alias;
                    if (courier.ContainsKey("lastName")) courier["lastName"] = string.Empty;
                    if (courier.ContainsKey("phone")) courier["phone"] = null;
                    if (courier.ContainsKey("email")) courier["email"] = null;
                    return courier;
                }
                var staffPerson = obj["firstName"] is JsonValue f && f.TryGetValue<string>(out var first) && obj["lastName"] is JsonValue l && l.TryGetValue<string>(out var last)
                                  && mask.StaffExact.Contains(Normalized($"{first} {last}"));
                var output = new JsonObject();
                foreach (var (entry, child) in obj)
                {
                    if (staffPerson && entry == "firstName") output[entry] = mask.StaffAlias;
                    else if (staffPerson && entry == "lastName") output[entry] = string.Empty;
                    else if (staffPerson && entry == "email") output[entry] = null;
                    else output[entry] = ContactKeys.Contains(entry) ? null : Apply(child?.DeepClone(), mask, entry);
                }
                return output;
            default:
                return node?.DeepClone();
        }
    }
}
