using Delivery.Api.Auth;
using Delivery.Application.Common.Models;
using Delivery.Application.Finance;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

/// <summary>`RemittancesController` : les retours de fonds (RF-), hors vendeur.</summary>
[Route("remittances")]
[Guards(Guard.NotMerchant)]
public sealed class RemittancesController(ISender sender) : ApiControllerBase(sender)
{
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static RemittanceListFilter Filter(ListQueryParams query, string? status, string[]? stage, string[]? courierId, string[]? cityId, string[]? agencyId, string? from, string? to)
    {
        query.Order ??= "desc";
        return new RemittanceListFilter(query.ToQueryWithDefault(25), Clean(status), MultiValue.AsList(stage), MultiValue.AsList(courierId), MultiValue.AsList(cityId), MultiValue.AsList(agencyId),
            Clean(from), Clean(to));
    }

    [HttpGet]
    [RequirePermission("finance.read")]
    public Task<IActionResult> List([FromQuery] ListQueryParams query, [FromQuery] string? status, [FromQuery] string[]? stage, [FromQuery] string[]? courierId, [FromQuery] string[]? cityId,
        [FromQuery] string[]? agencyId, [FromQuery] string? from, [FromQuery] string? to) =>
        Ok(new GetRemittancesQuery(Filter(query, status, stage, courierId, cityId, agencyId, from, to)));

    [HttpGet("export")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Export([FromQuery] ListQueryParams query, [FromQuery] string? status, [FromQuery] string[]? stage, [FromQuery] string[]? courierId, [FromQuery] string[]? cityId,
        [FromQuery] string[]? agencyId, [FromQuery] string? from, [FromQuery] string? to) =>
        FileOf(new ExportRemittancesQuery(Filter(query, status, stage, courierId, cityId, agencyId, from, to)));

    [HttpGet("candidates")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Candidates([FromQuery] string? courierId, [FromQuery] string? cityId, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? search) =>
        Ok(new GetRemittanceCandidatesQuery(courierId, Clean(cityId), Clean(from), Clean(to), Clean(search)));

    [HttpGet("summary")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Summary([FromQuery] string? period, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string[]? courierId, [FromQuery] string[]? agencyId) =>
        Ok(new GetRemittanceSummaryQuery(period, Clean(from), Clean(to), MultiValue.AsList(courierId), MultiValue.AsList(agencyId)));

    [HttpGet("{code}")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Detail(string code) => Ok(new GetRemittanceQuery(code));

    [HttpPost]
    [RequirePermission("finance.write")]
    public Task<IActionResult> Create([FromBody] CreateRemittanceCommand? body) => Created(body ?? new CreateRemittanceCommand());

    [HttpPost("{code}/parcels")]
    [RequirePermission("finance.write")]
    public Task<IActionResult> AddParcels(string code, [FromBody] AddRemittanceParcelsCommand? body) => Created((body ?? new AddRemittanceParcelsCommand()) with { Code = code });

    [HttpDelete("{code}/parcels/{parcelId}")]
    [RequirePermission("finance.write")]
    public Task<IActionResult> RemoveParcel(string code, string parcelId) => Ok(new RemoveRemittanceParcelCommand(code, parcelId));

    [HttpPatch("{code}/count")]
    [RequirePermission("finance.write")]
    public Task<IActionResult> Count(string code, [FromBody] CountRemittanceCommand? body) => Ok((body ?? new CountRemittanceCommand()) with { Code = code });

    [HttpGet("{code}/export")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> ExportParcels(string code) => FileOf(new ExportRemittanceParcelsQuery(code));

    [HttpGet("{code}/pdf")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Pdf(string code) => FileOf(new GetRemittancePdfQuery(code));

    [HttpPost("{code}/confirm")]
    [RequirePermission("finance.confirm")]
    public Task<IActionResult> Confirm(string code) => Created(new ConfirmRemittanceCommand(code));

    [HttpPost("{code}/reopen")]
    [RequirePermission("finance.confirm")]
    public Task<IActionResult> Reopen(string code) => Ok(new ReopenRemittanceCommand(code));

    [HttpDelete("{code}")]
    [RequirePermission("finance.write")]
    public Task<IActionResult> Remove(string code) => Ok(new DeleteRemittanceCommand(code));
}

/// <summary>`CourierBalanceController` : le solde d'écarts d'un livreur, lu et réglé par l'agence.</summary>
[Route("couriers")]
[Guards(Guard.NotMerchant)]
public sealed class CourierBalanceController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet("{id}/balance")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Balance(string id) => Ok(new GetCourierBalanceQuery(id));

    [HttpPost("{id}/balance/settlements")]
    [RequirePermission("finance.pay")]
    public Task<IActionResult> Settle(string id, [FromBody] SettleCourierBalanceCommand? body) => Created((body ?? new SettleCourierBalanceCommand()) with { CourierId = id });
}

/// <summary>`courier/me` côté argent : sa caisse ouverte, sa clôture, son solde.</summary>
[Route("courier/me")]
[Guards(Guard.Courier)]
public sealed class CourierCashController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet("balance")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Balance() => Ok(new GetMyBalanceQuery());

    [HttpGet("remittance")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Remittance() => Ok(new GetMyOpenRemittanceQuery());

    [HttpPost("remittance/close")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Close() => Ok(new CloseMyRemittanceCommand());
}
