using Delivery.Api.Auth;
using Delivery.Application.Bl;
using Delivery.Application.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

/// <summary>`BlController` : bons de livraison signés (BL) et bons de retour BL.</summary>
[Route("bl")]
public sealed class BlController(ISender sender) : ApiControllerBase(sender)
{
    private static DeliverySlipFilter SlipFilter(string? search, string? status, string[]? clientId, string[]? courierId, string? from, string? to) =>
        new(string.IsNullOrWhiteSpace(search) ? null : search.Trim(), string.IsNullOrWhiteSpace(status) ? null : status.Trim(), MultiValue.AsList(clientId), MultiValue.AsList(courierId),
            string.IsNullOrWhiteSpace(from) ? null : from.Trim(), string.IsNullOrWhiteSpace(to) ? null : to.Trim());

    /// <summary>Les listes BL se lisent de la plus récente à la plus ancienne par défaut (`order` vaut `desc`).</summary>
    private static ListQuery Paging(ListQueryParams query)
    {
        query.Order ??= "desc";
        return query.ToQueryWithDefault(25);
    }

    private static BlNoteFilter NoteFilter(ListQueryParams query, string[]? status, string[]? clientId, string[]? courierId, string[]? agencyId) =>
        new(Paging(query), MultiValue.AsList(status), MultiValue.AsList(clientId), MultiValue.AsList(courierId), MultiValue.AsList(agencyId));

    [HttpPost("parcels/{code}/collect")]
    [RequirePermission("returns.write")]
    public Task<IActionResult> Collect(string code, [FromBody] CollectBlCommand? body) => Created((body ?? new CollectBlCommand()) with { Code = code });

    [HttpGet("parcels/{code}")]
    [RequirePermission("returns.read")]
    public Task<IActionResult> Tracking(string code) => Ok(new GetParcelBlQuery(code));

    [HttpGet("delivery-slips")]
    [RequirePermission("returns.read")]
    public Task<IActionResult> Slips([FromQuery] ListQueryParams query, [FromQuery] string? status, [FromQuery] string[]? clientId, [FromQuery] string[]? courierId, [FromQuery] string? from,
        [FromQuery] string? to) =>
        Ok(new GetDeliverySlipsQuery(Paging(query), SlipFilter(query.Search, status, clientId, courierId, from, to)));

    [HttpGet("delivery-slips/export")]
    [RequirePermission("returns.read")]
    public Task<IActionResult> ExportSlips([FromQuery] ListQueryParams query, [FromQuery] string? status, [FromQuery] string[]? clientId, [FromQuery] string[]? courierId,
        [FromQuery] string? from, [FromQuery] string? to) =>
        FileOf(new ExportDeliverySlipsQuery(Paging(query), SlipFilter(query.Search, status, clientId, courierId, from, to)));

    [HttpPost("delivery-slips/advance")]
    [RequirePermission("returns.confirm")]
    public Task<IActionResult> Advance([FromBody] AdvanceDeliverySlipsCommand? body) => Created(body ?? new AdvanceDeliverySlipsCommand());

    [HttpPost("delivery-slips/{id}/revert")]
    [RequirePermission("returns.confirm")]
    public Task<IActionResult> Revert(string id, [FromBody] RevertDeliverySlipCommand? body) => Created((body ?? new RevertDeliverySlipCommand()) with { Id = id });

    [HttpGet("slips")]
    [Guards(Guard.NotMerchant)]
    [RequirePermission("returns.read")]
    public Task<IActionResult> Collectable([FromQuery] string? clientId, [FromQuery] string? courierId) =>
        Ok(new GetCollectableSlipsQuery(string.IsNullOrWhiteSpace(clientId) ? null : clientId.Trim(), string.IsNullOrWhiteSpace(courierId) ? null : courierId.Trim()));

    [HttpGet("notes")]
    [RequirePermission("returns.read")]
    public Task<IActionResult> Notes([FromQuery] ListQueryParams query, [FromQuery] string[]? status, [FromQuery] string[]? clientId, [FromQuery] string[]? courierId,
        [FromQuery] string[]? agencyId) =>
        Ok(new GetBlNotesQuery(NoteFilter(query, status, clientId, courierId, agencyId)));

    [HttpGet("notes/export")]
    [RequirePermission("returns.read")]
    public Task<IActionResult> ExportNotes([FromQuery] ListQueryParams query, [FromQuery] string[]? status, [FromQuery] string[]? clientId, [FromQuery] string[]? courierId,
        [FromQuery] string[]? agencyId) =>
        FileOf(new ExportBlNotesQuery(NoteFilter(query, status, clientId, courierId, agencyId)));

    [HttpGet("notes/{code}")]
    [RequirePermission("returns.read")]
    public Task<IActionResult> Note(string code) => Ok(new GetBlNoteQuery(code));

    [HttpPost("notes")]
    [RequirePermission("returns.write")]
    public Task<IActionResult> CreateNote([FromBody] CreateBlNoteCommand? body) => Created(body ?? new CreateBlNoteCommand());

    [HttpPost("notes/{code}/slips")]
    [RequirePermission("returns.write")]
    public Task<IActionResult> AddSlips(string code, [FromBody] AddBlNoteSlipsCommand? body) => Created((body ?? new AddBlNoteSlipsCommand()) with { Code = code });

    [HttpDelete("notes/{code}/slips/{slipId}")]
    [RequirePermission("returns.write")]
    public Task<IActionResult> RemoveSlip(string code, string slipId) => Ok(new RemoveBlNoteSlipCommand(code, slipId));

    [HttpPost("notes/{code}/handover")]
    [RequirePermission("returns.write")]
    public Task<IActionResult> HandOver(string code) => Created(new AdvanceBlNoteCommand { Code = code, Step = "handover" });

    [HttpPost("notes/{code}/receive")]
    [RequirePermission("returns.confirm")]
    public Task<IActionResult> Receive(string code) => Created(new AdvanceBlNoteCommand { Code = code, Step = "receive" });

    [HttpPost("notes/{code}/return")]
    [RequirePermission("returns.confirm")]
    public Task<IActionResult> Return(string code, [FromBody] AdvanceBlNoteCommand? body) => Created((body ?? new AdvanceBlNoteCommand()) with { Code = code, Step = "return" });

    [HttpPost("notes/{code}/close")]
    [RequirePermission("returns.confirm")]
    public Task<IActionResult> Close(string code) => Created(new AdvanceBlNoteCommand { Code = code, Step = "close" });

    [HttpGet("notes/{code}/pdf")]
    [RequirePermission("returns.read")]
    public Task<IActionResult> Pdf(string code) => FileOf(new GetBlNotePdfQuery(code));

    [HttpDelete("notes/{code}")]
    [RequirePermission("returns.write")]
    public Task<IActionResult> Remove(string code) => Ok(new DeleteBlNoteCommand(code));
}
