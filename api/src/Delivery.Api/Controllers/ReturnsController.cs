using Delivery.Api.Auth;
using Delivery.Application.Common.Models;
using Delivery.Application.Returns;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

/// <summary>`ReturnHandoversController` : les bons de retour livreur (RL-), hors vendeur.</summary>
[Route("return-handovers")]
[Guards(Guard.NotMerchant)]
public sealed class ReturnHandoversController(ISender sender) : ApiControllerBase(sender)
{
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static HandoverListFilter Filter(ListQueryParams query, string[]? status, string? origin, string[]? courierId, string[]? agencyId, string? from, string? to) =>
        new(query.ToQueryWithDefault(25), MultiValue.AsList(status), Clean(origin), MultiValue.AsList(courierId), MultiValue.AsList(agencyId), Clean(from), Clean(to));

    [HttpGet]
    [RequirePermission("returns.read")]
    public Task<IActionResult> List([FromQuery] ListQueryParams query, [FromQuery] string[]? status, [FromQuery] string? origin, [FromQuery] string[]? courierId, [FromQuery] string[]? agencyId,
        [FromQuery] string? from, [FromQuery] string? to) =>
        Ok(new GetReturnHandoversQuery(Filter(query, status, origin, courierId, agencyId, from, to)));

    [HttpGet("export")]
    [RequirePermission("returns.read")]
    public Task<IActionResult> Export([FromQuery] ListQueryParams query, [FromQuery] string[]? status, [FromQuery] string? origin, [FromQuery] string[]? courierId, [FromQuery] string[]? agencyId,
        [FromQuery] string? from, [FromQuery] string? to) =>
        FileOf(new ExportReturnHandoversQuery(Filter(query, status, origin, courierId, agencyId, from, to)));

    [HttpGet("candidates")]
    [RequirePermission("returns.read")]
    public Task<IActionResult> Candidates([FromQuery] string? courierId, [FromQuery] string? search, [FromQuery] string? page, [FromQuery] string? pageSize) =>
        Ok(new GetHandoverCandidatesQuery(courierId?.Trim(), Clean(search), page, pageSize));

    [HttpGet("bulk")]
    [RequirePermission("returns.read")]
    public Task<IActionResult> BulkPreview() => Ok(new GetHandoverBulkPreviewQuery());

    [HttpPost("bulk")]
    [RequirePermission("returns.write")]
    public Task<IActionResult> Bulk() => Created(new CreateHandoversInBulkCommand());

    [HttpGet("{code}")]
    [RequirePermission("returns.read")]
    public Task<IActionResult> Detail(string code) => Ok(new GetReturnHandoverQuery(code));

    [HttpPost]
    [RequirePermission("returns.write")]
    public Task<IActionResult> Create([FromBody] CreateReturnHandoverCommand? body) => Created(body ?? new CreateReturnHandoverCommand());

    [HttpPost("{code}/parcels")]
    [RequirePermission("returns.write")]
    public Task<IActionResult> Scan(string code, [FromBody] ScanHandoverParcelCommand? body) => Created((body ?? new ScanHandoverParcelCommand()) with { Handover = code });

    [HttpPost("{code}/parcels/batch")]
    [RequirePermission("returns.write")]
    public Task<IActionResult> AddParcels(string code, [FromBody] AddHandoverParcelsCommand? body) => Created((body ?? new AddHandoverParcelsCommand()) with { Code = code });

    [HttpDelete("{code}/parcels/{parcelId}")]
    [RequirePermission("returns.write")]
    public Task<IActionResult> RemoveParcel(string code, string parcelId) => Ok(new RemoveHandoverParcelCommand(code, parcelId));

    [HttpPost("{code}/request")]
    [RequirePermission("returns.write")]
    public Task<IActionResult> RequestReturn(string code) => Created(new RequestHandoverCommand(code));

    [HttpPost("{code}/pickup")]
    [RequirePermission("returns.write")]
    public Task<IActionResult> Pickup(string code, [FromBody] PickupHandoverCommand? body) => Created((body ?? new PickupHandoverCommand()) with { Code = code });

    [HttpPost("{code}/handover")]
    [RequirePermission("returns.write")]
    public Task<IActionResult> HandOver(string code) => Created(new HandOverHandoverCommand(code));

    [HttpPost("{code}/confirm")]
    [RequirePermission("returns.confirm")]
    public Task<IActionResult> Confirm(string code) => Created(new ConfirmHandoverCommand(code));

    [HttpPost("{code}/close")]
    [RequirePermission("returns.confirm")]
    public Task<IActionResult> Close(string code) => Created(new CloseHandoverCommand(code));

    [HttpPost("{code}/cancel")]
    [RequirePermission("returns.write")]
    public Task<IActionResult> Cancel(string code, [FromBody] CancelHandoverCommand? body) => Created((body ?? new CancelHandoverCommand()) with { Code = code });

    [HttpGet("{code}/export")]
    [RequirePermission("returns.read")]
    public Task<IActionResult> ExportParcels(string code) => FileOf(new ExportHandoverParcelsQuery(code));

    [HttpGet("{code}/pdf")]
    [RequirePermission("returns.read")]
    public Task<IActionResult> Pdf(string code) => FileOf(new GetHandoverPdfQuery(code));

    [HttpDelete("{code}")]
    [RequirePermission("returns.write")]
    public Task<IActionResult> Remove(string code) => Ok(new DeleteHandoverCommand(code));
}

/// <summary>`ReturnSlipsController` : les bons de retour remis à l'e-commerçant (BRT-).</summary>
[Route("return-slips")]
public sealed class ReturnSlipsController(ISender sender) : ApiControllerBase(sender)
{
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static SlipListFilter Filter(ListQueryParams query, string? status, string[]? clientId, string[]? agencyId, string? from, string? to) =>
        new(query.ToQueryWithDefault(25), Clean(status), MultiValue.AsList(clientId), MultiValue.AsList(agencyId), Clean(from), Clean(to));

    [HttpGet]
    [RequirePermission("returns.read")]
    public Task<IActionResult> List([FromQuery] ListQueryParams query, [FromQuery] string? status, [FromQuery] string[]? clientId, [FromQuery] string[]? agencyId, [FromQuery] string? from,
        [FromQuery] string? to) =>
        Ok(new GetReturnSlipsQuery(Filter(query, status, clientId, agencyId, from, to)));

    [HttpGet("export")]
    [RequirePermission("returns.read")]
    public Task<IActionResult> Export([FromQuery] ListQueryParams query, [FromQuery] string? status, [FromQuery] string[]? clientId, [FromQuery] string[]? agencyId, [FromQuery] string? from,
        [FromQuery] string? to) =>
        FileOf(new ExportReturnSlipsQuery(Filter(query, status, clientId, agencyId, from, to)));

    [HttpGet("bulk")]
    [Guards(Guard.NotMerchant)]
    [RequirePermission("returns.read")]
    public Task<IActionResult> BulkPreview() => Ok(new GetSlipBulkPreviewQuery());

    [HttpPost("bulk")]
    [RequirePermission("returns.write")]
    public Task<IActionResult> Bulk() => Created(new CreateSlipsInBulkCommand());

    [HttpGet("{code}")]
    [RequirePermission("returns.read")]
    public Task<IActionResult> Detail(string code) => Ok(new GetReturnSlipQuery(code));

    [HttpPost]
    [RequirePermission("returns.write")]
    public Task<IActionResult> Create([FromBody] CreateReturnSlipCommand? body) => Created(body ?? new CreateReturnSlipCommand());

    [HttpGet("{code}/candidates")]
    [RequirePermission("returns.read")]
    public Task<IActionResult> Candidates(string code, [FromQuery] string? search, [FromQuery] string? page, [FromQuery] string? pageSize) =>
        Ok(new GetSlipCandidatesQuery(code, Clean(search), page, pageSize));

    [HttpPost("{code}/parcels")]
    [RequirePermission("returns.write")]
    public Task<IActionResult> AddParcel(string code, [FromBody] AddSlipParcelCommand? body) => Created((body ?? new AddSlipParcelCommand()) with { Code = code });

    [HttpPost("{code}/parcels/batch")]
    [RequirePermission("returns.write")]
    public Task<IActionResult> AddParcels(string code, [FromBody] AddSlipParcelsCommand? body) => Created((body ?? new AddSlipParcelsCommand()) with { Code = code });

    [HttpDelete("{code}/parcels/{parcelId}")]
    [RequirePermission("returns.write")]
    public Task<IActionResult> RemoveParcel(string code, string parcelId) => Ok(new RemoveSlipParcelCommand(code, parcelId));

    [HttpGet("{code}/export")]
    [RequirePermission("returns.read")]
    public Task<IActionResult> ExportParcels(string code) => FileOf(new ExportSlipParcelsQuery(code));

    [HttpGet("{code}/pdf")]
    [RequirePermission("returns.read")]
    public Task<IActionResult> Pdf(string code) => FileOf(new GetSlipPdfQuery(code));

    [HttpPost("{code}/close")]
    [RequirePermission("returns.confirm")]
    public Task<IActionResult> Close(string code, [FromBody] CloseSlipCommand? body) => Created((body ?? new CloseSlipCommand()) with { Code = code });

    [HttpDelete("{code}")]
    [RequirePermission("returns.write")]
    public Task<IActionResult> Remove(string code) => Ok(new DeleteSlipCommand(code));
}

/// <summary>`ReturnsController` : les colis en attente de retour.</summary>
[Route("returns")]
public sealed class ReturnsController(ISender sender) : ApiControllerBase(sender)
{
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [HttpGet("pending")]
    [RequirePermission("returns.read")]
    public Task<IActionResult> Pending([FromQuery] string? agencyId, [FromQuery] string? courierId, [FromQuery] string? search) =>
        Ok(new GetPendingReturnsQuery(Clean(agencyId), Clean(courierId), Clean(search)));

    [HttpGet("pending/export")]
    [RequirePermission("returns.read")]
    public Task<IActionResult> ExportPending([FromQuery] string? agencyId, [FromQuery] string? courierId, [FromQuery] string? search) =>
        FileOf(new ExportPendingReturnsQuery(Clean(agencyId), Clean(courierId), Clean(search)));
}
