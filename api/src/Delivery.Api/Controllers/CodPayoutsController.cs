using Delivery.Api.Auth;
using Delivery.Application.Common.Models;
using Delivery.Application.Finance;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

/// <summary>`CodPayoutsController` : les bons de versement (BV-) du COD aux e-commerçants professionnels.</summary>
[Route("cod-payouts")]
public sealed class CodPayoutsController(ISender sender) : ApiControllerBase(sender)
{
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static CodPayoutListFilter Filter(ListQueryParams query, string? status, string[]? clientId, string[]? agencyId, string? from, string? to)
    {
        query.Order ??= "desc";
        return new CodPayoutListFilter(query.ToQueryWithDefault(25), Clean(status), MultiValue.AsList(clientId), MultiValue.AsList(agencyId), Clean(from), Clean(to));
    }

    [HttpGet]
    [RequirePermission("finance.read")]
    public Task<IActionResult> List([FromQuery] ListQueryParams query, [FromQuery] string? status, [FromQuery] string[]? clientId, [FromQuery] string[]? agencyId, [FromQuery] string? from,
        [FromQuery] string? to) =>
        Ok(new GetCodPayoutsQuery(Filter(query, status, clientId, agencyId, from, to)));

    [HttpGet("export")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Export([FromQuery] ListQueryParams query, [FromQuery] string? status, [FromQuery] string[]? clientId, [FromQuery] string[]? agencyId, [FromQuery] string? from,
        [FromQuery] string? to) =>
        FileOf(new ExportCodPayoutsQuery(Filter(query, status, clientId, agencyId, from, to)));

    [HttpGet("candidates")]
    [RequirePermission("finance.write")]
    public Task<IActionResult> Candidates([FromQuery] string? clientId, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? search) =>
        Ok(new GetCodPayoutCandidatesQuery(clientId, Clean(from), Clean(to), Clean(search)));

    [HttpGet("bulk-candidates")]
    [RequirePermission("finance.validate")]
    public Task<IActionResult> BulkCandidates([FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? agencyId) =>
        Ok(new GetCodPayoutBulkCandidatesQuery(Clean(from), Clean(to), Clean(agencyId)));

    [HttpPost("bulk")]
    [RequirePermission("finance.validate")]
    public Task<IActionResult> Bulk([FromBody] CreateCodPayoutsInBulkCommand? body) => Created(body ?? new CreateCodPayoutsInBulkCommand());

    [HttpGet("{code}")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Detail(string code) => Ok(new GetCodPayoutQuery(code));

    [HttpGet("{code}/export")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> ExportParcels(string code) => FileOf(new ExportCodPayoutParcelsQuery(code));

    [HttpGet("{code}/pdf")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Pdf(string code) => FileOf(new GetCodPayoutPdfQuery(code));

    [HttpPost]
    [RequirePermission("finance.write")]
    public Task<IActionResult> Create([FromBody] CreateCodPayoutCommand? body) => Created(body ?? new CreateCodPayoutCommand());

    [HttpPost("{code}/parcels")]
    [RequirePermission("finance.write")]
    public Task<IActionResult> AddParcels(string code, [FromBody] AddCodPayoutParcelsCommand? body) => Created((body ?? new AddCodPayoutParcelsCommand()) with { Code = code });

    [HttpDelete("{code}/parcels/{parcelId}")]
    [RequirePermission("finance.write")]
    public Task<IActionResult> RemoveParcel(string code, string parcelId) => Ok(new RemoveCodPayoutParcelCommand(code, parcelId));

    [HttpPost("{code}/pay")]
    [RequirePermission("finance.pay")]
    public Task<IActionResult> Pay(string code, [FromBody] PayCodPayoutCommand? body) => Created((body ?? new PayCodPayoutCommand()) with { Code = code });

    [HttpDelete("{code}")]
    [RequirePermission("finance.write")]
    public Task<IActionResult> Remove(string code) => Ok(new DeleteCodPayoutCommand(code));
}
