using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Delivery.Infrastructure.Persistence;

/// <summary>
/// Journal d'audit générique (`audit.extension.ts` de Speedy) : chaque création,
/// modification et suppression d'une entité métier écrit une ligne `audit_logs`
/// dans le même `SaveChanges` — états avant/après assainis, champs changés, acteur.
/// Une modification sans changement réel n'est pas tracée.
/// </summary>
public sealed class AuditInterceptor(ICurrentUser actor, IClock clock) : SaveChangesInterceptor
{
    /// <summary>Les modèles qui portaient `companyId` chez Speedy, moins `NEVER_AUDITED`.</summary>
    public static readonly HashSet<Type> Audited =
    [
        typeof(Agency), typeof(User), typeof(RolePermission), typeof(UserPermissionOverride), typeof(Zone), typeof(ZoneRate), typeof(DeliveryType),
        typeof(City), typeof(District), typeof(PricingRule), typeof(Client), typeof(ClientDiscount), typeof(ClientPickupAddress), typeof(ClientDocument),
        typeof(Courier), typeof(Parcel), typeof(ParcelEvent), typeof(ParcelNote), typeof(ParcelFeeChange), typeof(ParcelImport), typeof(Route),
        typeof(DeliveryNote), typeof(DeliveryNoteRemovedParcel), typeof(ReturnNote), typeof(DocumentSequence), typeof(ReturnHandover), typeof(ReturnSlip),
        typeof(DeliverySlip), typeof(BlNote), typeof(Bank), typeof(BankAccount), typeof(Invoice), typeof(InvoiceAdjustment), typeof(Payment),
        typeof(CashRemittance), typeof(CodPayout), typeof(CourierCashSession), typeof(CourierBalanceEntry), typeof(Exchange), typeof(Cheque),
        typeof(CourierStatement), typeof(Product), typeof(ProductAttribute), typeof(ProductAttributeValue), typeof(StockItem), typeof(StockMovement),
        typeof(StorageRequest), typeof(Order), typeof(ClaimType), typeof(Claim), typeof(ClaimEvent), typeof(ClaimAttachment), typeof(Announcement),
        typeof(FieldChange), typeof(Setting), typeof(PushToken), typeof(NotificationPreference), typeof(Notification), typeof(PasswordResetRequest),
        typeof(PettyCashMovement), typeof(PettyCashDay), typeof(TransferOrder), typeof(IdempotentRequest), typeof(ProviderToken),
    ];

    private static readonly HashSet<string> Redacted =
        ["passwordHash", "password", "token", "refreshToken", "resetToken", "apiKey", "secret", "tokenHash", "previousTokenHash", "rib", "nationalId"];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Record(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Record(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Record(DbContext? context)
    {
        if (context is null || AuditSilence.IsSilenced) return;
        context.ChangeTracker.DetectChanges();
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted && Audited.Contains(e.Metadata.ClrType))
            .ToList();
        foreach (var entry in entries)
        {
            var before = entry.State == EntityState.Added ? null : Snapshot(entry, original: true);
            var after = entry.State == EntityState.Deleted ? null : Snapshot(entry, original: false);
            var diff = Diff(before, after);
            if (entry.State == EntityState.Modified && diff.Count == 0) continue;
            var row = after ?? before!;
            context.Add(new AuditLog
            {
                Entity = entry.Metadata.ClrType.Name,
                EntityId = row.TryGetPropertyValue("id", out var id) && id is JsonValue v && v.TryGetValue<string>(out var text) ? text : string.Empty,
                Action = entry.State switch
                {
                    EntityState.Added => AuditAction.CREATE,
                    EntityState.Deleted => AuditAction.DELETE,
                    _ => AuditAction.UPDATE,
                },
                Before = before?.ToJsonString(),
                After = after?.ToJsonString(),
                Diff = diff,
                SubjectClientId = SubjectClientId(entry.Metadata.ClrType, after ?? before),
                UserId = actor.IsAuthenticated ? actor.UserId : null,
                UserEmail = actor.IsAuthenticated ? actor.Email : null,
                Role = actor.IsAuthenticated ? actor.Roles.FirstOrDefault().ToString() : null,
                Ip = actor.IpAddress,
                UserAgent = actor.UserAgent,
                RequestId = actor.RequestId,
                Source = actor.IsMobileClient ? "mobile" : "web",
                At = clock.UtcNow,
            });
        }
    }

    /// <summary>`sanitize` : secrets masqués, dates ISO, objets (Decimal, JSON) passés par `String()`.</summary>
    private static JsonObject Snapshot(EntityEntry entry, bool original)
    {
        var snapshot = new JsonObject();
        foreach (var clr in entry.Metadata.ClrType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var property = entry.Metadata.FindProperty(clr.Name);
            if (property is null) continue;
            var name = property.GetColumnName();
            if (Redacted.Contains(name))
            {
                snapshot[name] = "[redacted]";
                continue;
            }
            var value = original ? entry.Property(clr.Name).OriginalValue : entry.Property(clr.Name).CurrentValue;
            snapshot[name] = Wire(value, property.GetColumnType() == "jsonb");
        }
        return snapshot;
    }

    private static JsonNode? Wire(object? value, bool json) => value switch
    {
        null => null,
        string text when json => JsonText(text),
        string text => text,
        DateTime date => DateTime.SpecifyKind(date, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
        DateOnly day => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T00:00:00.000Z",
        decimal number => number.ToString("0.############################", CultureInfo.InvariantCulture),
        bool flag => flag,
        int number => number,
        long number => number,
        double number => number,
        Enum enumeration => enumeration.ToString(),
        IEnumerable<string> list => new JsonArray(list.Select(s => (JsonNode?)s).ToArray()),
        _ => value.ToString(),
    };

    /// <summary>Un `Json` Prisma : un objet devient « [object Object] », un tableau reste un tableau.</summary>
    private static JsonNode? JsonText(string text)
    {
        try
        {
            var node = JsonNode.Parse(text);
            return node switch
            {
                JsonObject => "[object Object]",
                JsonArray array => array.DeepClone(),
                _ => node?.DeepClone(),
            };
        }
        catch (JsonException)
        {
            return text;
        }
    }

    private static List<string> Diff(JsonObject? before, JsonObject? after)
    {
        if (before is null || after is null) return [];
        return before.Select(p => p.Key).Union(after.Select(p => p.Key))
            .Where(key => key != "updatedAt")
            .Where(key => (before[key]?.ToJsonString() ?? "null") != (after[key]?.ToJsonString() ?? "null"))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static string? SubjectClientId(Type type, JsonObject? row)
    {
        if (row is null) return null;
        if (type == typeof(Client)) return row["id"]?.GetValue<string>();
        return row.TryGetPropertyValue("clientId", out var clientId) && clientId is JsonValue v && v.TryGetValue<string>(out var text) ? text : null;
    }
}
