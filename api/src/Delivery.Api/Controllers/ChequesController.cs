using Delivery.Api.Auth;
using Delivery.Application.Common.Models;
using Delivery.Application.Finance;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

/// <summary>`ChequesController` et `ChequeSlipController` : le suivi des chèques et leurs bons de remise.</summary>
[Route("cheques")]
public sealed class ChequesController(ISender sender) : ApiControllerBase(sender)
{
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ChequeListFilter Filter(ListQueryParams query, string[]? status, string[]? courierId, string[]? clientId, string? bank, string? from, string? to) =>
        new(query.ToQueryWithDefault(25), MultiValue.AsList(status), MultiValue.AsList(courierId), MultiValue.AsList(clientId), Clean(bank), Clean(from), Clean(to));

    [HttpGet]
    [RequirePermission("finance.read")]
    public Task<IActionResult> List([FromQuery] ListQueryParams query, [FromQuery] string[]? status, [FromQuery] string[]? courierId, [FromQuery] string[]? clientId, [FromQuery] string? bank,
        [FromQuery] string? from, [FromQuery] string? to) =>
        Ok(new GetChequesQuery(Filter(query, status, courierId, clientId, bank, from, to)));

    [HttpGet("export")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Export([FromQuery] ListQueryParams query, [FromQuery] string[]? status, [FromQuery] string[]? courierId, [FromQuery] string[]? clientId, [FromQuery] string? bank,
        [FromQuery] string? from, [FromQuery] string? to) =>
        FileOf(new ExportChequesQuery(Filter(query, status, courierId, clientId, bank, from, to)));

    [HttpPost("slip")]
    [Guards(Guard.NotMerchant)]
    [RequirePermission("finance.read")]
    public Task<IActionResult> GroupSlip([FromBody] GetChequeGroupSlipQuery? body) => CreatedFile(body ?? new GetChequeGroupSlipQuery());

    [HttpPost("{id}/slip")]
    [Guards(Guard.NotMerchant)]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Slip(string id) => CreatedFile(new GetChequeSlipQuery(id));

    [HttpGet("{id}")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Detail(string id) => Ok(new GetChequeQuery(id));

    [HttpPost("bulk-advance")]
    [Guards(Guard.NotMerchant)]
    [RequirePermission("finance.read")]
    public Task<IActionResult> BulkAdvance([FromBody] AdvanceChequesCommand? body) => Created(body ?? new AdvanceChequesCommand());

    [HttpPost("{id}/advance")]
    [Guards(Guard.NotMerchant)]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Advance(string id, [FromBody] ChequeTransitionCommand? body) => Created((body ?? new ChequeTransitionCommand()) with { Id = id, Action = "advance" });

    [HttpPost("{id}/reject")]
    [Guards(Guard.NotMerchant)]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Reject(string id, [FromBody] ChequeTransitionCommand? body) => Created((body ?? new ChequeTransitionCommand()) with { Id = id, Action = "reject" });

    [HttpPost("{id}/cancel")]
    [Guards(Guard.NotMerchant)]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Cancel(string id, [FromBody] ChequeTransitionCommand? body) => Created((body ?? new ChequeTransitionCommand()) with { Id = id, Action = "cancel" });
}
