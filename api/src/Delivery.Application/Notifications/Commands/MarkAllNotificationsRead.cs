using Delivery.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Notifications;

/// <summary>`markAllRead` : tout est lu, la pastille tombe à zéro.</summary>
public sealed record MarkAllNotificationsReadCommand : IRequest<UnreadDto>;

internal sealed class MarkAllNotificationsReadHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<MarkAllNotificationsReadCommand, UnreadDto>
{
    public async Task<UnreadDto> Handle(MarkAllNotificationsReadCommand input, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        await db.Notifications.Where(n => n.UserId == caller.UserId && n.ReadAt == null).ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), cancellationToken);
        return new UnreadDto(0);
    }
}
