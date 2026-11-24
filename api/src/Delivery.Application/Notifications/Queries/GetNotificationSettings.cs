using Delivery.Application.Common.Interfaces;
using MediatR;

namespace Delivery.Application.Notifications;

/// <summary>`settings` : les sujets de l'audience du compte, critiques verrouillés.</summary>
public sealed record GetNotificationSettingsQuery : IRequest<NotificationSettingsDto>;

internal sealed class GetNotificationSettingsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetNotificationSettingsQuery, NotificationSettingsDto>
{
    public Task<NotificationSettingsDto> Handle(GetNotificationSettingsQuery input, CancellationToken cancellationToken) => NotificationCenter.SettingsAsync(db, caller, cancellationToken);
}
