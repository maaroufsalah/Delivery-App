using System.Text.Json;
using Delivery.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Notifications;

/// <summary>`feed` : les 50 dernières lignes (sujet et paramètres, jamais le texte rendu) et le nombre de non lues.</summary>
public sealed record GetNotificationFeedQuery : IRequest<NotificationFeedDto>;

internal sealed class GetNotificationFeedHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetNotificationFeedQuery, NotificationFeedDto>
{
    public async Task<NotificationFeedDto> Handle(GetNotificationFeedQuery input, CancellationToken cancellationToken)
    {
        var visible = await NotificationCenter.VisibleAsync(db, caller, cancellationToken);
        var rows = await visible.OrderByDescending(n => n.CreatedAt).Take(50).Select(n => new { n.Id, n.Topic, n.Params, n.Data, n.ReadAt, n.CreatedAt }).ToListAsync(cancellationToken);
        var unread = await visible.CountAsync(n => n.ReadAt == null, cancellationToken);
        return new NotificationFeedDto(rows.Select(r => new NotificationItemDto(r.Id, r.Topic, Json(r.Params), Json(r.Data), r.ReadAt, r.CreatedAt)).ToList(), unread);
    }

    /// <summary>`row.params ?? {}`.</summary>
    private static JsonElement Json(string? value)
    {
        using var document = JsonDocument.Parse(string.IsNullOrEmpty(value) || value == "null" ? "{}" : value);
        return document.RootElement.Clone();
    }
}
