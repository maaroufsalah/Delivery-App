using Delivery.Api.Auth;
using Delivery.Application.Common.Models;
using Delivery.Application.Warehouse.Stock;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

[Route("stock")]
public sealed class StockController(ISender sender) : ApiControllerBase(sender)
{
    private static StockListFilter Filter(ListQueryParams query, string? order, string[]? clientId, string[]? agencyId, string? lowStock) =>
        new(query.ToQueryWithDefault(25), order == "desc" ? "desc" : "asc", MultiValue.AsList(clientId), MultiValue.AsList(agencyId), lowStock);

    [HttpGet]
    [RequirePermission("warehouse.read")]
    public Task<IActionResult> List([FromQuery] ListQueryParams query, [FromQuery] string? order, [FromQuery] string[]? clientId, [FromQuery] string[]? agencyId, [FromQuery] string? lowStock) =>
        Ok(new GetStockQuery(Filter(query, order, clientId, agencyId, lowStock)));

    [HttpGet("export")]
    [RequirePermission("warehouse.read")]
    public Task<IActionResult> Export([FromQuery] ListQueryParams query, [FromQuery] string? order, [FromQuery] string[]? clientId, [FromQuery] string[]? agencyId, [FromQuery] string? lowStock) =>
        FileOf(new ExportStockQuery(Filter(query, order, clientId, agencyId, lowStock)));

    [HttpGet("{id}")]
    [RequirePermission("warehouse.read")]
    public Task<IActionResult> Detail(string id) => Ok(new GetStockItemQuery(id));

    [HttpPost("adjust")]
    [RequirePermission("warehouse.write")]
    public Task<IActionResult> Adjust([FromBody] AdjustStockCommand? body) => Created(body ?? new AdjustStockCommand());
}
