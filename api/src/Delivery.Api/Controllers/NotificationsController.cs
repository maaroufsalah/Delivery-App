using Delivery.Application.Notifications;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

/// <summary>`notifications.controller.ts` : l'appareil, le centre de notifications et les réglages du compte.</summary>
[Route("me")]
public sealed class NotificationsController(ISender sender) : ApiControllerBase(sender)
{
    [HttpPost("push-token")]
    public Task<IActionResult> Register([FromBody] RegisterPushTokenCommand body) => Ok(body);

    [HttpDelete("push-token")]
    public Task<IActionResult> Forget([FromBody] ForgetPushTokenCommand body) => Ok(body);

    [HttpGet("notification-feed")]
    public Task<IActionResult> Feed() => Ok(new GetNotificationFeedQuery());

    [HttpPost("notification-feed/read-all")]
    public Task<IActionResult> MarkAllRead() => Ok(new MarkAllNotificationsReadCommand());

    [HttpPost("notification-feed/{id}/read")]
    public Task<IActionResult> MarkRead(string id) => Ok(new MarkNotificationReadCommand(id));

    [HttpGet("notifications")]
    public Task<IActionResult> Settings() => Ok(new GetNotificationSettingsQuery());

    [HttpPatch("notifications")]
    public Task<IActionResult> SetPreference([FromBody] SetNotificationPreferenceCommand body) => Ok(body);
}
