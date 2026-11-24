using Delivery.Api.Auth;
using Delivery.Application.Common.Models;
using Delivery.Application.Finance;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

/// <summary>`CourierStatementsController` : les relevés de rémunération des livreurs (PAI-), hors vendeur.</summary>
[Route("courier-statements")]
[Guards(Guard.NotMerchant)]
public sealed class CourierStatementsController(ISender sender) : ApiControllerBase(sender)
{
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static StatementListFilter Filter(ListQueryParams query, string? status, string? courierId, string? agencyId, string? from, string? to)
    {
        query.Order ??= "desc";
        return new StatementListFilter(query.ToQueryWithDefault(25), Clean(status), Clean(courierId), Clean(agencyId), Clean(from), Clean(to));
    }

    [HttpGet]
    [RequirePermission("finance.read")]
    public Task<IActionResult> List([FromQuery] ListQueryParams query, [FromQuery] string? status, [FromQuery] string? courierId, [FromQuery] string? agencyId, [FromQuery] string? from,
        [FromQuery] string? to) =>
        Ok(new GetCourierStatementsQuery(Filter(query, status, courierId, agencyId, from, to)));

    [HttpGet("export")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Export([FromQuery] ListQueryParams query, [FromQuery] string? status, [FromQuery] string? courierId, [FromQuery] string? agencyId, [FromQuery] string? from,
        [FromQuery] string? to) =>
        FileOf(new ExportCourierStatementsQuery(Filter(query, status, courierId, agencyId, from, to)));

    [HttpGet("{code}")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Detail(string code) => Ok(new GetCourierStatementQuery(code));

    [HttpGet("{code}/pdf")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Pdf(string code) => FileOf(new GetCourierStatementPdfQuery(code));

    [HttpPost]
    [RequirePermission("finance.write")]
    public Task<IActionResult> Create([FromBody] CreateCourierStatementCommand? body) => Created(body ?? new CreateCourierStatementCommand());

    [HttpPost("{code}/recompute")]
    [RequirePermission("finance.write")]
    public Task<IActionResult> Recompute(string code) => Created(new RecomputeCourierStatementCommand(code));

    [HttpPost("{code}/validate")]
    [RequirePermission("finance.validate")]
    public Task<IActionResult> Validate(string code) => Created(new ValidateCourierStatementCommand(code));

    [HttpPost("{code}/pay")]
    [RequirePermission("finance.pay")]
    public Task<IActionResult> Pay(string code, [FromBody] PayCourierStatementCommand? body) => Created((body ?? new PayCourierStatementCommand()) with { Code = code });

    [HttpGet("{code}/cheque-photo")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> ChequePhoto(string code) => FileOf(new GetStatementChequePhotoQuery(code));

    [HttpPost("{code}/cheque-photo")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> AttachChequePhoto(string code, [FromBody] AttachStatementChequePhotoCommand? body) =>
        Created((body ?? new AttachStatementChequePhotoCommand()) with { Code = code });

    [HttpDelete("{code}")]
    [RequirePermission("finance.write")]
    public Task<IActionResult> Remove(string code) => Ok(new DeleteCourierStatementCommand(code));
}

/// <summary>`courier/me/cheques` : les chèques de paie du livreur, et le dépôt de leur photo.</summary>
[Route("courier/me/cheques")]
[Guards(Guard.Courier)]
public sealed class CourierChequesController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [RequirePermission("finance.read")]
    public Task<IActionResult> List() => Ok(new GetMyChequesQuery());

    [HttpPost]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Deposit([FromBody] DepositMyChequeCommand? body) => Ok(body ?? new DepositMyChequeCommand());
}
