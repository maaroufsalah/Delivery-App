using Delivery.Api.Auth;
using Delivery.Application.CourierApp;
using Delivery.Application.Couriers.Queries;
using Delivery.Application.Routes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

[Route("courier/me")]
[Guards(Guard.Courier)]
public sealed class CourierController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> Me() => Ok(new GetCourierMeQuery());

    [HttpGet("performance")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> Performance([FromQuery] string? period) => Ok(new GetCourierPerformanceQuery(period));

    [HttpGet("notes")]
    [RequirePermission("routes.read")]
    public Task<IActionResult> Notes([FromQuery] string? kind, [FromQuery] string? status) => Ok(new GetCourierNotesQuery(kind, status));

    [HttpGet("routes")]
    [RequirePermission("routes.read")]
    public Task<IActionResult> Routes([FromQuery] string? status, [FromQuery] string? from, [FromQuery] string? kind) => Ok(new GetCourierRoutesQuery(status, from, kind));

    [HttpGet("deliveries")]
    [RequirePermission("routes.read")]
    public Task<IActionResult> Deliveries() => Ok(new GetCourierDeliveriesQuery());

    [HttpGet("routes/{code}")]
    [RequirePermission("routes.read")]
    public Task<IActionResult> Route(string code) => Ok(new GetCourierRouteQuery(Uri.UnescapeDataString(code)));

    [HttpPost("scan")]
    [RequirePermission("routes.scan")]
    public Task<IActionResult> Scan([FromBody] CourierScanCommand? body, [FromHeader(Name = "idempotency-key")] string? idempotencyKey) =>
        Ok((body ?? new CourierScanCommand()) with { IdempotencyKey = idempotencyKey });

    [HttpDelete("routes/{code}/parcels/{parcelId}")]
    [RequirePermission("routes.scan")]
    public Task<IActionResult> RemoveParcel(string code, string parcelId) => Ok(new RemoveCourierRouteParcelCommand(Uri.UnescapeDataString(code), parcelId));

    [HttpPost("routes/{code}/dispatch")]
    [RequirePermission("routes.confirm")]
    public Task<IActionResult> Dispatch(string code) => Ok(new DispatchCourierRouteCommand(Uri.UnescapeDataString(code)));

    [HttpPost("routes/{code}/notes/{noteId}/pickup")]
    [RequirePermission("routes.confirm")]
    public Task<IActionResult> PickupNote(string code, string noteId, [FromBody] PickupRouteNoteCommand? body) =>
        Ok((body ?? new PickupRouteNoteCommand()) with { Code = Uri.UnescapeDataString(code), NoteId = noteId });
}

[Route("courier/parcels")]
[Guards(Guard.Courier)]
public sealed class CourierParcelsController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet("{code}/outcomes")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> Outcomes(string code) => Ok(new GetCourierOutcomesQuery(Uri.UnescapeDataString(code)));

    [HttpPost("{code}/status")]
    [RequirePermission("parcels.status")]
    public Task<IActionResult> Report(string code, [FromBody] ReportCourierOutcomeCommand? body, [FromHeader(Name = "idempotency-key")] string? idempotencyKey) =>
        Ok((body ?? new ReportCourierOutcomeCommand()) with { Code = Uri.UnescapeDataString(code), IdempotencyKey = idempotencyKey });

    [HttpPost("{code}/proof")]
    [RequirePermission("parcels.status")]
    public Task<IActionResult> Proof(string code, [FromBody] AttachCourierProofCommand? body, [FromHeader(Name = "idempotency-key")] string? idempotencyKey) =>
        Ok((body ?? new AttachCourierProofCommand()) with { Code = Uri.UnescapeDataString(code), IdempotencyKey = idempotencyKey });
}
