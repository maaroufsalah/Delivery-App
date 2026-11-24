using Delivery.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Notifications;

/// <summary>`markRead` : une ligne lue (sans effet si elle l'était ou n'est pas à soi) ; renvoie la pastille.</summary>
public sealed record MarkNotificationReadCommand(string Id) : IRequest<UnreadDto>;

internal sealed class MarkNotificationReadHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<MarkNotificationReadCommand, UnreadDto>
{
    public async Task<UnreadDto> Handle(MarkNotificationReadCommand input, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        await db.Notifications.Where(n => n.UserId == caller.UserId && n.Id == input.Id && n.ReadAt == null).ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), cancellationToken);
        return await NotificationCenter.UnreadAsync(db, caller, cancellationToken);
    }
}
