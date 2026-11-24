using Delivery.Api.Auth;
using Delivery.Application.BulkJobs;
using Delivery.Application.Common.Models;
using Delivery.Application.DeliveryNotes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

[Route("delivery-notes")]
public sealed class DeliveryNotesController(ISender sender) : ApiControllerBase(sender)
{
    private static NoteListFilter Filter(ListQueryParams query, string? kind, string[]? status, string[]? clientId) =>
        new(query.ToQueryWithDefault(10), kind, MultiValue.AsList(status), MultiValue.AsList(clientId));

    [HttpGet]
    [RequirePermission("notes.read")]
    public Task<IActionResult> List([FromQuery] ListQueryParams query, [FromQuery] string? kind, [FromQuery] string[]? status, [FromQuery] string[]? clientId) =>
        Ok(new GetNotesQuery(Filter(query, kind, status, clientId)));

    [HttpGet("stats")]
    [RequirePermission("notes.read")]
    public Task<IActionResult> Stats([FromQuery] ListQueryParams query, [FromQuery] string? kind, [FromQuery] string[]? status, [FromQuery] string[]? clientId) =>
        Ok(new GetNoteStatsQuery(Filter(query, kind, status, clientId)));

    [HttpGet("export")]
    [RequirePermission("notes.read")]
    public Task<IActionResult> Export([FromQuery] ListQueryParams query, [FromQuery] string? kind, [FromQuery] string[]? status, [FromQuery] string[]? clientId) =>
        FileOf(new ExportNotesQuery(Filter(query, kind, status, clientId)));

    [HttpPost]
    [RequirePermission("notes.write")]
    public Task<IActionResult> Create([FromBody] CreateNoteCommand? body) => Created(body ?? new CreateNoteCommand());

    [HttpGet("{code}")]
    [RequirePermission("notes.read")]
    public Task<IActionResult> Detail(string code) => Ok(new GetNoteQuery(code));

    [HttpGet("{code}/export")]
    [RequirePermission("notes.read")]
    public Task<IActionResult> ExportParcels(string code) => FileOf(new ExportNoteParcelsQuery(code));

    [HttpPost("{code}/parcels")]
    [RequirePermission("notes.write")]
    public Task<IActionResult> AddParcels(string code, [FromBody] AddNoteParcelsCommand? body) => Created((body ?? new AddNoteParcelsCommand()) with { Code = code });

    [HttpDelete("{code}/parcels/{parcelId}")]
    [RequirePermission("notes.write")]
    public Task<IActionResult> RemoveParcel(string code, string parcelId) => Ok(new RemoveNoteParcelCommand(code, parcelId));

    [HttpPost("{code}/validate")]
    [RequirePermission("notes.write")]
    public Task<IActionResult> Validate(string code, [FromBody] ValidateNoteCommand? body, [FromHeader(Name = BulkJobsHeader.Name)] string? bulk) =>
        Accepted((body ?? new ValidateNoteCommand()) with { Code = code, Async = bulk == "1" });

    [HttpPost("{code}/removed/{parcelId}/restore")]
    [RequirePermission("notes.write")]
    public Task<IActionResult> RestoreRemoved(string code, string parcelId) => Created(new RestoreNoteParcelCommand(code, parcelId));

    [HttpDelete("{code}")]
    [RequirePermission("notes.write")]
    public Task<IActionResult> Remove(string code) => Ok(new DeleteNoteCommand(code));
}

[Route("bulk-jobs")]
[Guards(Guard.NotMerchant)]
public sealed class BulkJobsController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [RequirePermission("parcels.read")]
    public async Task<IActionResult> Latest([FromQuery] string? target)
    {
        // Nest répond 200 avec un corps vide quand il n'y a pas de tâche (et non 204).
        var job = await Sender.Send(new GetLatestBulkJobQuery(target), HttpContext.RequestAborted);
        return job is null ? StatusCode(StatusCodes.Status200OK) : base.Ok(job);
    }

    [HttpGet("{id}")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> One(string id) => Ok(new GetBulkJobQuery(id));

    [HttpPost("{id}/retry")]
    [RequirePermission("parcels.status")]
    public Task<IActionResult> Retry(string id) => Ok(new RetryBulkJobCommand(id));
}
