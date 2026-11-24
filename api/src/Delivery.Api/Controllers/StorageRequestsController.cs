using Delivery.Api.Auth;
using Delivery.Application.Common.Models;
using Delivery.Application.Warehouse.Storage;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

[Route("storage-requests")]
public sealed class StorageRequestsController(ISender sender) : ApiControllerBase(sender)
{
    private static StorageListFilter Filter(ListQueryParams query, string? order, string[]? clientId, string[]? agencyId, string[]? status) =>
        new(query.ToQueryWithDefault(25), order == "desc" ? "desc" : "asc", MultiValue.AsList(clientId), MultiValue.AsList(agencyId), MultiValue.AsList(status));

    [HttpGet]
    [RequirePermission("warehouse.read")]
    public Task<IActionResult> List([FromQuery] ListQueryParams query, [FromQuery] string? order, [FromQuery] string[]? clientId, [FromQuery] string[]? agencyId, [FromQuery] string[]? status) =>
        Ok(new GetStorageRequestsQuery(Filter(query, order, clientId, agencyId, status)));

    [HttpGet("export")]
    [RequirePermission("warehouse.read")]
    public Task<IActionResult> Export([FromQuery] ListQueryParams query, [FromQuery] string? order, [FromQuery] string[]? clientId, [FromQuery] string[]? agencyId, [FromQuery] string[]? status) =>
        FileOf(new ExportStorageRequestsQuery(Filter(query, order, clientId, agencyId, status)));

    [HttpGet("{code}")]
    [RequirePermission("warehouse.read")]
    public Task<IActionResult> Detail(string code) => Ok(new GetStorageRequestQuery(code));

    [HttpGet("{code}/receipt")]
    [RequirePermission("warehouse.read")]
    public Task<IActionResult> Receipt(string code) => FileOf(new GetStorageReceiptQuery(code));

    [HttpPost]
    [RequirePermission("warehouse.write")]
    public Task<IActionResult> Create([FromBody] CreateStorageRequestCommand? body) => Created(body ?? new CreateStorageRequestCommand());

    [HttpPost("{code}/decision")]
    [RequirePermission("warehouse.validate")]
    public Task<IActionResult> Decide(string code, [FromBody] DecideStorageRequestCommand? body) => Created((body ?? new DecideStorageRequestCommand()) with { Code = code });

    [HttpDelete("{code}")]
    [RequirePermission("warehouse.write")]
    public Task<IActionResult> Remove(string code) => Ok(new RemoveStorageRequestCommand(code));
}
