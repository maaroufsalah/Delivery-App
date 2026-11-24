using Delivery.Api.Auth;
using Delivery.Application.Common.Models;
using Delivery.Application.Exchanges.Commands;
using Delivery.Application.Exchanges.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

[Route("exchanges")]
public sealed class ExchangesController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> List([FromQuery] ListQueryParams query, [FromQuery] string[]? status) =>
        Ok(new GetExchangesQuery(query.ToQueryWithDefault(20), MultiValue.AsList(status)));

    [HttpGet("{code}")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> Detail(string code) => Ok(new GetExchangeQuery(code));

    [HttpPost]
    [RequirePermission("parcels.write")]
    [Guards(Guard.NotMerchant)]
    public Task<IActionResult> Create([FromBody] CreateExchangeCommand? body) => Created(body ?? new CreateExchangeCommand());

    [HttpPost("{code}/pickup")]
    [RequirePermission("parcels.write")]
    [Guards(Guard.NotMerchant)]
    public Task<IActionResult> Pickup(string code, [FromBody] PickupExchangeCommand? body) => Created((body ?? new PickupExchangeCommand()) with { Code = code });

    [HttpPost("{code}/deliver")]
    [RequirePermission("parcels.write")]
    [Guards(Guard.NotMerchant)]
    public Task<IActionResult> Deliver(string code, [FromBody] DeliverExchangeCommand? body) => Created((body ?? new DeliverExchangeCommand()) with { Code = code });

    [HttpPost("{code}/close")]
    [RequirePermission("parcels.write")]
    [Guards(Guard.NotMerchant)]
    public Task<IActionResult> Close(string code, [FromBody] CloseExchangeCommand? body) => Created((body ?? new CloseExchangeCommand()) with { Code = code });

    [HttpPost("{code}/cancel")]
    [RequirePermission("parcels.write")]
    [Guards(Guard.NotMerchant)]
    public Task<IActionResult> Cancel(string code, [FromBody] CancelExchangeCommand? body) => Created((body ?? new CancelExchangeCommand()) with { Code = code });
}
