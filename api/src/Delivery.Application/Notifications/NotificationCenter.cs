using System.Text.Json;
using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Notifications;

/// <summary>Ce que `NotificationsService` partage entre ses lectures : l'audience du compte et les lignes qu'il voit.</summary>
public static class NotificationCenter
{
    /// <summary>`audienceOf` : livreur, vendeur, ou personnel (STAFF, SUPER_ADMIN) ; sinon aucun réglage.</summary>
    public static string? AudienceOf(ICurrentUser caller)
    {
        if (caller.CourierId is not null) return "courier";
        if (caller.ClientId is not null) return "merchant";
        return caller.Roles.Contains(RoleName.STAFF) || caller.Roles.Contains(RoleName.SUPER_ADMIN) ? "staff" : null;
    }

    public static async Task<NotificationSettingsDto> SettingsAsync(IAppDbContext db, ICurrentUser caller, CancellationToken cancellationToken)
    {
        var audience = AudienceOf(caller);
        if (audience is null) return new NotificationSettingsDto(null, []);
        var disabled = (await db.NotificationPreferences.AsNoTracking().Where(p => p.UserId == caller.UserId && !p.Enabled).Select(p => p.Topic).ToListAsync(cancellationToken)).ToHashSet();
        return new NotificationSettingsDto(audience,
            Notifier.Topics.Where(t => t.Audience == audience).Select(t => new NotificationTopicSettingDto(t.Key, t.Critical || !disabled.Contains(t.Key), t.Critical)).ToList());
    }

    /// <summary>`visibleWhere` : un livreur ne revoit pas l'affectation d'un circuit déjà clos.</summary>
    public static async Task<IQueryable<Notification>> VisibleAsync(IAppDbContext db, ICurrentUser caller, CancellationToken cancellationToken)
    {
        var mine = db.Notifications.AsNoTracking().Where(n => n.UserId == caller.UserId);
        if (caller.CourierId is null) return mine;
        var assigned = await mine.Where(n => n.Topic == "route-assigned").Select(n => new { n.Id, n.Data }).ToListAsync(cancellationToken);
        var withCode = assigned.Select(a => (a.Id, Code: RouteCode(a.Data))).Where(a => a.Code is not null).ToList();
        if (withCode.Count == 0) return mine;
        var codes = withCode.Select(a => a.Code!).Distinct().ToList();
        var closed = (await db.Routes.AsNoTracking().Where(r => codes.Contains(r.Code) && r.Status == RouteStatus.CLOSED).Select(r => r.Code).ToListAsync(cancellationToken)).ToHashSet();
        var hidden = withCode.Where(a => closed.Contains(a.Code!)).Select(a => a.Id).ToList();
        return hidden.Count == 0 ? mine : mine.Where(n => !hidden.Contains(n.Id));
    }

    private static string? RouteCode(string data)
    {
        try
        {
            using var document = JsonDocument.Parse(data);
            return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("routeCode", out var code) && code.ValueKind == JsonValueKind.String
                ? code.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static async Task<UnreadDto> UnreadAsync(IAppDbContext db, ICurrentUser caller, CancellationToken cancellationToken) =>
        new(await (await VisibleAsync(db, caller, cancellationToken)).CountAsync(n => n.ReadAt == null, cancellationToken));
}
