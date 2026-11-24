using Delivery.Api.Auth;
using Delivery.Application.Common.Models;
using Delivery.Application.Warehouse.Products;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

[Route("products")]
public sealed class ProductsController(ISender sender) : ApiControllerBase(sender)
{
    private static ProductListFilter Filter(ListQueryParams query, string? order, string[]? clientId, string[]? agencyId, string? isActive) =>
        new(query.ToQueryWithDefault(25), order == "desc" ? "desc" : "asc", MultiValue.AsList(clientId), MultiValue.AsList(agencyId), isActive);

    [HttpGet]
    [RequirePermission("warehouse.read")]
    public Task<IActionResult> List([FromQuery] ListQueryParams query, [FromQuery] string? order, [FromQuery] string[]? clientId, [FromQuery] string[]? agencyId, [FromQuery] string? isActive) =>
        Ok(new GetProductsQuery(Filter(query, order, clientId, agencyId, isActive)));

    [HttpGet("export")]
    [RequirePermission("warehouse.read")]
    public Task<IActionResult> Export([FromQuery] ListQueryParams query, [FromQuery] string? order, [FromQuery] string[]? clientId, [FromQuery] string[]? agencyId, [FromQuery] string? isActive) =>
        FileOf(new ExportProductsQuery(Filter(query, order, clientId, agencyId, isActive)));

    [HttpGet("attributes")]
    [RequirePermission("warehouse.read")]
    public Task<IActionResult> Attributes() => Ok(new GetProductAttributesQuery());

    [HttpPost("attributes")]
    [RequirePermission("warehouse.write")]
    public Task<IActionResult> CreateAttribute([FromBody] CreateProductAttributeCommand? body) => Created(body ?? new CreateProductAttributeCommand());

    [HttpPost("attributes/values")]
    [RequirePermission("warehouse.write")]
    public Task<IActionResult> CreateAttributeValue([FromBody] CreateProductAttributeValueCommand? body) => Created(body ?? new CreateProductAttributeValueCommand());

    [HttpGet("{id}")]
    [RequirePermission("warehouse.read")]
    public Task<IActionResult> Detail(string id) => Ok(new GetProductQuery(id));

    [HttpGet("{id}/image")]
    [RequirePermission("warehouse.read")]
    public async Task<IActionResult> Image(string id)
    {
        var file = await Sender.Send(new GetProductImageQuery(id), HttpContext.RequestAborted);
        Response.Headers.CacheControl = file.CacheControl;
        return File(file.Bytes, file.ContentType);
    }

    [HttpPost]
    [RequirePermission("warehouse.write")]
    public Task<IActionResult> Create([FromBody] CreateProductCommand? body) => Created(body ?? new CreateProductCommand());

    [HttpPatch("{id}")]
    [RequirePermission("warehouse.write")]
    public Task<IActionResult> Update(string id, [FromBody] UpdateProductCommand? body) => Ok((body ?? new UpdateProductCommand()) with { Id = id });

    [HttpPost("{id}/image")]
    [RequirePermission("warehouse.write")]
    public Task<IActionResult> SetImage(string id, [FromBody] SetProductImageCommand? body) => Created((body ?? new SetProductImageCommand()) with { Id = id });

    [HttpDelete("{id}")]
    [RequirePermission("warehouse.write")]
    public Task<IActionResult> Remove(string id) => Ok(new RemoveProductCommand(id));
}
