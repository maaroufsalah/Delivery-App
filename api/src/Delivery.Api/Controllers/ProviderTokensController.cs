using Delivery.Api.Auth;
using Delivery.Application.Providers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

[Route("settings/providers")]
public sealed class ProviderTokensController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [RequirePermission("settings.read")]
    public Task<IActionResult> List() => Ok(new GetProviderTokensQuery());

    [HttpPost]
    [RequirePermission("settings.write")]
    public Task<IActionResult> Create([FromBody] CreateProviderTokenCommand? body) => Created(body ?? new CreateProviderTokenCommand());

    [HttpPost("{id}/rotate")]
    [RequirePermission("settings.write")]
    public Task<IActionResult> Rotate(string id) => Created(new RotateProviderTokenCommand(id));

    [HttpPost("{id}/revoke")]
    [RequirePermission("settings.write")]
    public Task<IActionResult> Revoke(string id) => Created(new RevokeProviderTokenCommand(id));

    [HttpPatch("{id}/mode")]
    [RequirePermission("settings.write")]
    public Task<IActionResult> SetMode(string id, [FromBody] SetProviderModeCommand? body) => Ok((body ?? new SetProviderModeCommand()) with { Id = id });

    [HttpPut("{id}/status-map")]
    [RequirePermission("settings.write")]
    public Task<IActionResult> SetStatusMap(string id, [FromBody] SetProviderStatusMapCommand? body) => Ok((body ?? new SetProviderStatusMapCommand()) with { Id = id });

    [HttpPost("{id}/calls/{callId}/resolve")]
    [RequirePermission("settings.write")]
    public Task<IActionResult> ResolveAlert(string id, string callId) => Created(new ResolveProviderAlertCommand(id, callId));

    [HttpGet("{id}/calls")]
    [RequirePermission("settings.read")]
    public Task<IActionResult> Calls(string id) => Ok(new GetProviderCallsQuery(id));
}
