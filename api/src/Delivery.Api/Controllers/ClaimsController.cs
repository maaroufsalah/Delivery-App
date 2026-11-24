using Delivery.Api.Auth;
using Delivery.Application.Claims;
using Delivery.Application.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

/// <summary>`claims.controller.ts` : réclamations, leur fil, leurs pièces jointes et le référentiel des types.</summary>
[Route("claims")]
public sealed class ClaimsController(ISender sender) : ApiControllerBase(sender)
{
    private static ClaimListFilter Filter(string? search, string[]? status, string[]? typeId, string[]? clientId, string? assigneeId, string? late, string? page, string? pageSize) =>
        new(search, MultiValue.AsList(status), MultiValue.AsList(typeId), MultiValue.AsList(clientId), assigneeId, late, page, pageSize);

    [HttpGet]
    [RequirePermission("claims.read")]
    public Task<IActionResult> List([FromQuery] string? search, [FromQuery] string[]? status, [FromQuery] string[]? typeId, [FromQuery] string[]? clientId, [FromQuery] string? assigneeId,
        [FromQuery] string? late, [FromQuery] string? page, [FromQuery] string? pageSize) =>
        Ok(new ListClaimsQuery(Filter(search, status, typeId, clientId, assigneeId, late, page, pageSize)));

    [HttpGet("export")]
    [RequirePermission("claims.read")]
    public Task<IActionResult> Export([FromQuery] string? search, [FromQuery] string[]? status, [FromQuery] string[]? typeId, [FromQuery] string[]? clientId, [FromQuery] string? assigneeId,
        [FromQuery] string? late) =>
        FileOf(new ExportClaimsQuery(Filter(search, status, typeId, clientId, assigneeId, late, null, null)));

    [HttpGet("counters")]
    [RequirePermission("claims.read")]
    public Task<IActionResult> Counters() => Ok(new GetClaimCountersQuery());

    [HttpGet("types")]
    [RequirePermission("claims.read")]
    public Task<IActionResult> Types() => Ok(new ListClaimTypesQuery());

    [HttpPost("types")]
    [RequirePermission("settings.write")]
    public Task<IActionResult> CreateType([FromBody] CreateClaimTypeCommand? body) => Created(body ?? new CreateClaimTypeCommand());

    [HttpPatch("types/{id}")]
    [RequirePermission("settings.write")]
    public Task<IActionResult> UpdateType(string id, [FromBody] UpdateClaimTypeCommand? body) => Ok((body ?? new UpdateClaimTypeCommand()) with { Id = id });

    [HttpDelete("types/{id}")]
    [RequirePermission("settings.write")]
    public Task<IActionResult> RemoveType(string id) => Ok(new DeleteClaimTypeCommand(id));

    [HttpPost]
    [RequirePermission("claims.write")]
    public Task<IActionResult> Create([FromBody] CreateClaimCommand? body) => Created(body ?? new CreateClaimCommand());

    [HttpGet("{code}")]
    [RequirePermission("claims.read")]
    public Task<IActionResult> Detail(string code) => Ok(new GetClaimQuery(code));

    [HttpPost("{code}/events")]
    [RequirePermission("claims.write")]
    public Task<IActionResult> Reply(string code, [FromBody] ReplyClaimCommand? body) => Created((body ?? new ReplyClaimCommand()) with { Code = code });

    [HttpPatch("{code}/assignee")]
    [RequirePermission("claims.handle")]
    public Task<IActionResult> Assign(string code, [FromBody] AssignClaimCommand? body) => Ok((body ?? new AssignClaimCommand()) with { Code = code });

    [HttpGet("{code}/attachments/{id}")]
    [RequirePermission("claims.read")]
    public Task<IActionResult> Attachment(string code, string id) => FileOf(new GetClaimAttachmentQuery(code, id));
}
