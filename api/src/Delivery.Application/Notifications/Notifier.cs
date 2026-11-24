using System.Text.Json;
using Delivery.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Notifications;

public sealed record NotificationTopic(string Key, string Audience, bool Critical);

/// <summary>
/// `NotificationsService.notifyMerchant` / `dispatch` : une ligne par destinataire,
/// sauf préférence coupée (les sujets critiques passent toujours).
/// </summary>
public static class Notifier
{
    public static readonly IReadOnlyList<NotificationTopic> Topics =
    [
        new("route-assigned", "courier", true),
        new("route-withdrawn", "courier", true),
        new("return-assigned", "courier", true),
        new("remittance-confirmed", "courier", false),
        new("return-note-requested", "courier", true),
        new("return-note-received", "courier", false),
        new("parcel-destination-changed", "courier", true),
        new("parcel-transferred-out", "courier", true),
        new("parcel-transferred-in", "courier", true),
        new("delivery-cancelled", "courier", true),
        new("parcel-delivered", "merchant", false),
        new("parcel-delivery-cancelled", "merchant", false),
        new("parcel-rerouted", "merchant", true),
        new("parcel-failed", "merchant", false),
        new("parcel-postponed", "merchant", false),
        new("invoice-issued", "merchant", false),
        new("claim-answered", "merchant", true),
        new("bl-ready", "merchant", false),
        new("bl-returned", "merchant", false),
        new("note-parcels-removed", "merchant", true),
        new("parcel-deleted", "merchant", true),
        new("cash-variance", "staff", false),
        new("claim-opened", "staff", false),
        new("stale-route", "staff", false),
        new("return-note-awaiting", "staff", false),
        new("bl-note-awaiting", "staff", false),
    ];

    public static NotificationTopic? Find(string key) => Topics.FirstOrDefault(t => t.Key == key);

    public static async Task NotifyMerchantAsync(IAppDbContext db, string clientId, string topic, IReadOnlyDictionary<string, object> parameters, IReadOnlyDictionary<string, string> data, CancellationToken cancellationToken)
    {
        var userId = await db.Users.AsNoTracking().Where(u => u.Client != null && u.Client.Id == clientId && u.IsActive && u.DeletedAt == null)
            .Select(u => u.Id).FirstOrDefaultAsync(cancellationToken);
        await DispatchAsync(db, userId is null ? [] : [userId], topic, parameters, data, cancellationToken);
    }

    public static async Task NotifyCourierAsync(IAppDbContext db, string courierId, string topic, IReadOnlyDictionary<string, object> parameters, IReadOnlyDictionary<string, string> data, CancellationToken cancellationToken)
    {
        var userId = await db.Users.AsNoTracking().Where(u => u.Courier != null && u.Courier.Id == courierId && u.IsActive && u.DeletedAt == null)
            .Select(u => u.Id).FirstOrDefaultAsync(cancellationToken);
        await DispatchAsync(db, userId is null ? [] : [userId], topic, parameters, data, cancellationToken);
    }

    public static async Task DispatchAsync(IAppDbContext db, IReadOnlyList<string> userIds, string topic, IReadOnlyDictionary<string, object> parameters, IReadOnlyDictionary<string, string> data, CancellationToken cancellationToken)
    {
        var definition = Find(topic);
        if (userIds.Count == 0 || definition is null) return;
        var refused = definition.Critical
            ? []
            : (await db.NotificationPreferences.AsNoTracking().Where(p => userIds.Contains(p.UserId) && p.Topic == topic && !p.Enabled)
                .Select(p => p.UserId).ToListAsync(cancellationToken)).ToHashSet();
        var wanted = userIds.Where(id => !refused.Contains(id)).ToList();
        if (wanted.Count == 0) return;
        var paramsJson = JsonSerializer.Serialize(parameters);
        var dataJson = JsonSerializer.Serialize(data);
        foreach (var userId in wanted)
        {
            db.Notifications.Add(new Domain.Entities.Notification { UserId = userId, Topic = topic, Params = paramsJson, Data = dataJson });
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
