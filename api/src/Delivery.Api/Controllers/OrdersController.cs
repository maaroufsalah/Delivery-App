using Delivery.Api.Auth;
using Delivery.Application.Common.Models;
using Delivery.Application.Warehouse.Orders;
using Delivery.Application.Warehouse.Imports;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

[Route("orders")]
public sealed class OrdersController(ISender sender) : ApiControllerBase(sender)
{
    private static OrderListFilter Filter(ListQueryParams query, string? order, string[]? clientId, string[]? agencyId, string[]? status, string? source) =>
        new(query.ToQueryWithDefault(25), order == "desc" ? "desc" : "asc", MultiValue.AsList(clientId), MultiValue.AsList(agencyId), MultiValue.AsList(status), source);

    [HttpGet]
    [RequirePermission("warehouse.read")]
    public Task<IActionResult> List([FromQuery] ListQueryParams query, [FromQuery] string? order, [FromQuery] string[]? clientId, [FromQuery] string[]? agencyId, [FromQuery] string[]? status, [FromQuery] string? source) =>
        Ok(new GetOrdersQuery(Filter(query, order, clientId, agencyId, status, source)));

    [HttpGet("export")]
    [RequirePermission("warehouse.read")]
    public Task<IActionResult> Export([FromQuery] ListQueryParams query, [FromQuery] string? order, [FromQuery] string[]? clientId, [FromQuery] string[]? agencyId, [FromQuery] string[]? status, [FromQuery] string? source) =>
        FileOf(new ExportOrdersQuery(Filter(query, order, clientId, agencyId, status, source)));

    [HttpPost("import")]
    [RequirePermission("warehouse.write")]
    public Task<IActionResult> Import([FromBody] ImportOrdersCommand? body) => Created(body ?? new ImportOrdersCommand());

    [HttpGet("{code}")]
    [RequirePermission("warehouse.read")]
    public Task<IActionResult> Detail(string code) => Ok(new GetOrderQuery(code));

    [HttpPost]
    [RequirePermission("warehouse.write")]
    public Task<IActionResult> Create([FromBody] CreateOrderCommand? body) => Created(body ?? new CreateOrderCommand());

    [HttpPost("{code}/confirm")]
    [RequirePermission("warehouse.write")]
    public Task<IActionResult> Confirm(string code) => Created(new ConfirmOrderCommand(code));

    [HttpPost("{code}/parcel")]
    [RequirePermission("warehouse.write")]
    public Task<IActionResult> CreateParcel(string code, [FromBody] CreateOrderParcelCommand? body) => Created((body ?? new CreateOrderParcelCommand()) with { Code = code });

    [HttpPost("{code}/cancel")]
    [RequirePermission("warehouse.write")]
    public Task<IActionResult> Cancel(string code) => Created(new CancelOrderCommand(code));

    [HttpDelete("{code}")]
    [RequirePermission("warehouse.write")]
    public Task<IActionResult> Remove(string code) => Ok(new RemoveOrderCommand(code));
}
