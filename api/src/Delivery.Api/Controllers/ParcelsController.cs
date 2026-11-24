using Delivery.Api.Auth;
using Delivery.Application.Parcels;
using Delivery.Application.Parcels.Commands;
using Delivery.Application.Parcels.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

[Route("parcels")]
public sealed partial class ParcelsController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> List([FromQuery] ParcelListParams query) => Ok(new GetParcelsQuery(query.ToFilter()));

    [HttpGet("export")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> Export([FromQuery] ParcelListParams query) => FileOf(new ExportParcelsQuery(query.ToFilter()));

    [HttpGet("stats")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> Stats([FromQuery] ParcelListParams query) => Ok(new GetParcelStatsQuery(query.ToFilter()));

    [HttpGet("next-code")]
    [RequirePermission("parcels.write")]
    public Task<IActionResult> NextCode([FromQuery] GetNextParcelCodeQuery query) => Ok(query);

    [HttpPost("quote")]
    [RequirePermission("parcels.write")]
    public Task<IActionResult> Quote([FromBody] QuoteParcelQuery? body) => Created(body ?? new QuoteParcelQuery());

    [HttpPost]
    [RequirePermission("parcels.write")]
    public Task<IActionResult> Create([FromBody] CreateParcelCommand? body, [FromHeader(Name = "idempotency-key")] string? idempotencyKey) =>
        Created((body ?? new CreateParcelCommand()) with { IdempotencyKey = idempotencyKey });

    [HttpGet("{code}")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> Detail(string code, [FromQuery] string? deleted) => Ok(new GetParcelQuery(code, deleted == "true"));

    [HttpPost("{code}/events")]
    [RequirePermission("parcels.status")]
    [Guards(Guard.NotMerchant)]
    public Task<IActionResult> ChangeStatus(string code, [FromBody] ChangeParcelStatusCommand? body) =>
        Created((body ?? new ChangeParcelStatusCommand()) with { Code = code });
}
