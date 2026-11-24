using Delivery.Api.Auth;
using Delivery.Application.Common.Models;
using Delivery.Application.Routes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

[Route("routes")]
public sealed class RoutesController(ISender sender) : ApiControllerBase(sender)
{
    private static RouteListFilter Filter(ListQueryParams query, string[]? status, string[]? cityId, string? courierId, string? kind, string? recent) =>
        new(query.ToQueryWithDefault(10), MultiValue.AsList(status), MultiValue.AsList(cityId), courierId, kind, recent);

    [HttpGet]
    [RequirePermission("routes.read")]
    public Task<IActionResult> List([FromQuery] ListQueryParams query, [FromQuery] string[]? status, [FromQuery] string[]? cityId, [FromQuery] string? courierId,
        [FromQuery] string? kind, [FromQuery] string? recent) =>
        Ok(new GetRoutesQuery(Filter(query, status, cityId, courierId, kind, recent)));

    [HttpGet("stats")]
    [RequirePermission("routes.read")]
    public Task<IActionResult> Stats([FromQuery] ListQueryParams query, [FromQuery] string[]? status, [FromQuery] string[]? cityId, [FromQuery] string? courierId,
        [FromQuery] string? kind, [FromQuery] string? recent) =>
        Ok(new GetRouteStatsQuery(Filter(query, status, cityId, courierId, kind, recent)));

    [HttpGet("export")]
    [RequirePermission("routes.read")]
    public Task<IActionResult> Export([FromQuery] ListQueryParams query, [FromQuery] string[]? status, [FromQuery] string[]? cityId, [FromQuery] string? courierId,
        [FromQuery] string? kind) =>
        FileOf(new ExportRoutesQuery(Filter(query, status, cityId, courierId, kind, null)));

    [HttpPost]
    [RequirePermission("routes.write")]
    public Task<IActionResult> Create([FromBody] CreateRouteCommand? body) => Created(body ?? new CreateRouteCommand());

    [HttpGet("{code}")]
    [RequirePermission("routes.read")]
    public Task<IActionResult> Detail(string code) => Ok(new GetRouteQuery(Uri.UnescapeDataString(code)));

    [HttpPatch("{code}")]
    [RequirePermission("routes.write")]
    public Task<IActionResult> Update(string code, [FromBody] UpdateRouteCommand? body) => Ok((body ?? new UpdateRouteCommand()) with { Code = Uri.UnescapeDataString(code) });

    [HttpDelete("{code}")]
    [RequirePermission("routes.write")]
    public Task<IActionResult> Remove(string code) => Ok(new DeleteRouteCommand(Uri.UnescapeDataString(code)));

    [HttpGet("{code}/candidates")]
    [RequirePermission("routes.read")]
    public Task<IActionResult> Candidates(string code, [FromQuery] string? search, [FromQuery] string? district, [FromQuery] string? cityId, [FromQuery] string? page,
        [FromQuery] string? pageSize) =>
        Ok(new GetRouteCandidatesQuery(Uri.UnescapeDataString(code), search, district, cityId, page, pageSize));

    [HttpGet("{code}/note-candidates")]
    [RequirePermission("routes.read")]
    public Task<IActionResult> NoteCandidates(string code, [FromQuery] string? search, [FromQuery] string? page, [FromQuery] string? pageSize) =>
        Ok(new GetRouteNoteCandidatesQuery(Uri.UnescapeDataString(code), search, page, pageSize));

    [HttpPost("{code}/notes")]
    [RequirePermission("routes.write")]
    public Task<IActionResult> AddNotes(string code, [FromBody] AddRouteNotesCommand? body) =>
        Created((body ?? new AddRouteNotesCommand()) with { Code = Uri.UnescapeDataString(code) });

    [HttpDelete("{code}/notes/{noteId}")]
    [RequirePermission("routes.write")]
    public Task<IActionResult> RemoveNote(string code, string noteId) => Ok(new RemoveRouteNoteCommand(Uri.UnescapeDataString(code), noteId));

    [HttpPost("{code}/notes/{noteId}/pickup")]
    [RequirePermission("routes.confirm")]
    public Task<IActionResult> PickupNote(string code, string noteId, [FromBody] PickupRouteNoteCommand? body) =>
        Created((body ?? new PickupRouteNoteCommand()) with { Code = Uri.UnescapeDataString(code), NoteId = noteId });

    [HttpPost("{code}/parcels")]
    [RequirePermission("routes.write")]
    public Task<IActionResult> AddParcels(string code, [FromBody] AddRouteParcelsCommand? body) =>
        Created((body ?? new AddRouteParcelsCommand()) with { Code = Uri.UnescapeDataString(code) });

    [HttpDelete("{code}/parcels/{parcelId}")]
    [RequirePermission("routes.write")]
    public Task<IActionResult> RemoveParcel(string code, string parcelId) => Ok(new RemoveRouteParcelCommand(Uri.UnescapeDataString(code), parcelId));

    [HttpGet("{code}/export")]
    [RequirePermission("routes.read")]
    public Task<IActionResult> ExportParcels(string code) => FileOf(new ExportRouteParcelsQuery(Uri.UnescapeDataString(code)));
}
