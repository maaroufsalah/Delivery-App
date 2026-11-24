using System.Text.Json.Nodes;
using Delivery.Api.Auth;
using Delivery.Application.Settings.Commands;
using Delivery.Application.Settings.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

[Route("settings")]
public sealed class SettingsController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet("company")]
    [RequirePermission("settings.read")]
    public Task<IActionResult> GetCompany() => Ok(new GetCompanySettingsQuery());

    [HttpGet("business")]
    [RequirePermission("settings.read")]
    public Task<IActionResult> GetBusiness() => Ok(new GetBusinessSettingsQuery());

    [HttpPatch("business")]
    [RequirePermission("settings.write")]
    public Task<IActionResult> UpdateBusiness([FromBody] UpdateBusinessSettingsCommand? body) => Ok(body ?? new UpdateBusinessSettingsCommand());

    [HttpGet("finance")]
    [RequirePermission("settings.read")]
    public Task<IActionResult> GetFinance() => Ok(new GetFinanceSettingsQuery());

    [HttpPatch("finance")]
    [RequirePermission("settings.write")]
    public Task<IActionResult> UpdateFinance([FromBody] UpdateFinanceSettingsCommand? body) => Ok(body ?? new UpdateFinanceSettingsCommand());

    [HttpPost("logo")]
    [RequirePermission("settings.write")]
    public Task<IActionResult> UploadLogo([FromBody] UploadLogoCommand? body) => Created(body ?? new UploadLogoCommand());

    [HttpPatch("company")]
    [RequirePermission("settings.write")]
    public Task<IActionResult> UpdateCompany([FromBody] JsonNode? body) => Ok(new UpdateCompanySettingsCommand(body));

    [HttpGet("theme")]
    [RequirePermission("settings.read")]
    public Task<IActionResult> GetTheme() => Ok(new GetThemeWorkbenchQuery());

    [HttpPost("theme/restore/{which}")]
    [RequirePermission("settings.write")]
    public Task<IActionResult> RestoreTheme(string which) => Created(new RestoreThemeCommand(which));

    [HttpPatch("theme")]
    [RequirePermission("settings.write")]
    public Task<IActionResult> UpdateTheme([FromBody] JsonNode? body) => Ok(new UpdateThemeCommand(body));
}
