using Delivery.Api.Auth;
using Delivery.Application.Common.Models;
using Delivery.Application.Roles.Commands;
using Delivery.Application.Roles.Queries;
using Delivery.Application.Users.Commands;
using Delivery.Application.Users.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

[Route("users")]
public sealed class UsersController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [RequirePermission("accounts.read")]
    public Task<IActionResult> List([FromQuery] ListQueryParams query, [FromQuery] string[]? role, [FromQuery] string[]? agencyId) =>
        Ok(new GetUsersQuery(query.ToQuery(), MultiValue.AsList(role), MultiValue.AsList(agencyId)));

    [HttpGet("export")]
    [RequirePermission("accounts.read")]
    public Task<IActionResult> Export([FromQuery] ListQueryParams query, [FromQuery] string[]? role, [FromQuery] string[]? agencyId) =>
        FileOf(new ExportUsersQuery(new GetUsersQuery(query.ToQuery(), MultiValue.AsList(role), MultiValue.AsList(agencyId))));

    [HttpPost("me/password")]
    [AllowPasswordChange]
    public Task<IActionResult> ChangePassword([FromBody] ChangeOwnPasswordCommand? body) => Created(body ?? new ChangeOwnPasswordCommand());

    [HttpPatch("me")]
    public Task<IActionResult> UpdateOwnProfile([FromBody] UpdateOwnProfileCommand? body) => Ok(body ?? new UpdateOwnProfileCommand());

    [HttpGet("team")]
    [RequirePermission("accounts.read")]
    public Task<IActionResult> Team([FromQuery] ListQueryParams query) => Ok(new GetTeamQuery(query.ToQuery()));

    [HttpGet("{id}")]
    [RequirePermission("accounts.read")]
    public Task<IActionResult> Detail(string id) => Ok(new GetUserQuery(id));

    [HttpPost]
    [RequirePermission("accounts.write")]
    public Task<IActionResult> Create([FromBody] CreateUserCommand? body) => Created(body ?? new CreateUserCommand());

    [HttpPatch("{id}")]
    [RequirePermission("accounts.write")]
    public Task<IActionResult> Update(string id, [FromBody] UpdateUserCommand? body) => Ok((body ?? new UpdateUserCommand()) with { Id = id });

    [HttpPatch("{id}/active")]
    [RequirePermission("accounts.write")]
    public Task<IActionResult> SetActive(string id, [FromBody] SetUserActiveCommand? body) => Ok((body ?? new SetUserActiveCommand()) with { Id = id });

    [HttpPost("{id}/reset-password")]
    [RequirePermission("accounts.write")]
    public Task<IActionResult> ResetPassword(string id) => Created(new ResetUserPasswordCommand(id));

    [HttpPost("{id}/set-password")]
    [RequirePermission("accounts.write")]
    public Task<IActionResult> SetPassword(string id, [FromBody] SetUserPasswordCommand? body) => Created((body ?? new SetUserPasswordCommand()) with { Id = id });

    [HttpDelete("{id}")]
    [RequirePermission("accounts.write")]
    public Task<IActionResult> Remove(string id) => Ok(new RemoveUserCommand(id));

    [HttpGet("{id}/permissions")]
    [RequirePermission("accounts.read")]
    public Task<IActionResult> Permissions(string id) => Ok(new GetUserPermissionsQuery(id));

    [HttpPut("{id}/permissions")]
    [RequirePermission("accounts.write")]
    public Task<IActionResult> UpdatePermissions(string id, [FromBody] UpdateUserPermissionsCommand? body) =>
        Ok((body ?? new UpdateUserPermissionsCommand()) with { UserId = id });

    [HttpDelete("{id}/permissions")]
    [RequirePermission("accounts.write")]
    public Task<IActionResult> ResetPermissions(string id) => Ok(new ResetUserPermissionsCommand(id));
}

[Route("roles")]
public sealed class RolesController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [RequirePermission("accounts.read")]
    public Task<IActionResult> Matrix() => Ok(new GetRoleMatrixQuery());

    [HttpPut]
    [RequirePermission("accounts.write")]
    public Task<IActionResult> Update([FromBody] UpdateRoleMatrixCommand? body) => Ok(body ?? new UpdateRoleMatrixCommand());

    [HttpPost("reload")]
    [RequirePermission("accounts.write")]
    public Task<IActionResult> Reload() => Created(new GetRoleMatrixQuery());
}
