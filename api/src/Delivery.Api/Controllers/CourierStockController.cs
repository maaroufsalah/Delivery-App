using Delivery.Api.Auth;
using Delivery.Application.Common.Models;
using Delivery.Application.CourierStock;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

/// <summary>`courier-stock.controller.ts` : « Stock livreur », lecture seule (D-222).</summary>
[Route("courier-stock")]
public sealed class CourierStockController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [RequirePermission("routes.stock")]
    public Task<IActionResult> List([FromQuery] ListQueryParams query, [FromQuery] string[]? agencyId, [FromQuery] string? all) =>
        Ok(new GetCourierStockQuery(query, agencyId, all));

    [HttpGet("export")]
    [RequirePermission("routes.stock")]
    public Task<IActionResult> Export([FromQuery] ListQueryParams query, [FromQuery] string[]? agencyId, [FromQuery] string? all)
    {
        Response.Headers.CacheControl = "private, no-store";
        return FileOf(new ExportCourierStockQuery(query, agencyId, all));
    }

    [HttpGet("pdf")]
    [RequirePermission("routes.stock")]
    public Task<IActionResult> GlobalPdf([FromQuery] ListQueryParams query, [FromQuery] string[]? agencyId, [FromQuery] string? all) =>
        FileOf(new GetCourierStockPdfQuery(query, agencyId, all));

    [HttpPost("pdf-jobs")]
    [RequirePermission("routes.stock")]
    public async Task<IActionResult> CreatePdfJob([FromBody] System.Text.Json.JsonElement? body) =>
        StatusCode(StatusCodes.Status202Accepted, await Sender.Send(new CreateCourierStockPdfJobCommand(body), HttpContext.RequestAborted));

    [HttpGet("pdf-jobs/{id}")]
    [RequirePermission("routes.stock")]
    public Task<IActionResult> PdfJob(string id)
    {
        Response.Headers.CacheControl = "private, no-store";
        return Ok(new Delivery.Application.Parcels.Labels.GetLabelJobQuery(id));
    }

    [HttpGet("pdf-jobs/{id}/file")]
    [RequirePermission("routes.stock")]
    public Task<IActionResult> PdfJobFile(string id) => FileOf(new Delivery.Application.Parcels.Labels.GetLabelJobFileQuery(id));

    [HttpGet("parcels/export")]
    [RequirePermission("routes.stock")]
    public Task<IActionResult> ExportParcels([FromQuery] ListQueryParams query, [FromQuery] string? courierId, [FromQuery] string? statusId, [FromQuery] string[]? clientId,
        [FromQuery] string[]? cityId, [FromQuery] string[]? routeId)
    {
        Response.Headers.CacheControl = "private, no-store";
        return FileOf(new ExportCourierStockParcelsQuery(courierId, query, statusId, clientId, cityId, routeId));
    }

    [HttpGet("{courierId}/pdf")]
    [RequirePermission("routes.stock")]
    public Task<IActionResult> CourierPdf(string courierId, [FromQuery] ListQueryParams query, [FromQuery] string? statusId, [FromQuery] string[]? clientId, [FromQuery] string[]? cityId,
        [FromQuery] string[]? routeId) =>
        FileOf(new GetCourierStockCourierPdfQuery(courierId, query, statusId, clientId, cityId, routeId));

    [HttpGet("{courierId}")]
    [RequirePermission("routes.stock")]
    public Task<IActionResult> Detail(string courierId, [FromQuery] ListQueryParams query, [FromQuery] string? statusId, [FromQuery] string[]? clientId, [FromQuery] string[]? cityId,
        [FromQuery] string[]? routeId) =>
        Ok(new GetCourierStockDetailQuery(courierId, query, statusId, clientId, cityId, routeId));
}
