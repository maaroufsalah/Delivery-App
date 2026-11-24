using Delivery.Api.Auth;
using Delivery.Application.Exchanges.Commands;
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

    [HttpPatch("{code}/fees")]
    [RequirePermission("parcels.editFees")]
    public Task<IActionResult> EditFees(string code, [FromBody] EditParcelFeesCommand? body) => Ok((body ?? new EditParcelFeesCommand()) with { Code = code });

    [HttpPatch("{code}")]
    [RequirePermission("parcels.write")]
    [Guards(Guard.Merchant)]
    public Task<IActionResult> UpdateBySeller(string code, [FromBody] UpdateParcelBySellerCommand? body) => Ok((body ?? new UpdateParcelBySellerCommand()) with { Code = code });

    [HttpDelete("{code}")]
    [RequirePermission("parcels.write")]
    [Guards(Guard.Merchant)]
    public Task<IActionResult> RemoveBySeller(string code) => Ok(new RemoveParcelBySellerCommand(code));

    [HttpPatch("{code}/expected-payment")]
    [RequirePermission("parcels.write")]
    [Guards(Guard.NotMerchant)]
    public Task<IActionResult> ChangeExpectedPayment(string code, [FromBody] ChangeExpectedPaymentCommand? body) => Ok((body ?? new ChangeExpectedPaymentCommand()) with { Code = code });

    [HttpPatch("{code}/bl")]
    [RequirePermission("parcels.write")]
    public Task<IActionResult> SetBlRequested(string code, [FromBody] SetParcelBlRequestedCommand? body) => Ok((body ?? new SetParcelBlRequestedCommand()) with { Code = code });

    [HttpPost("{code}/notes")]
    [RequirePermission("parcels.write")]
    [Guards(Guard.NotMerchant)]
    public Task<IActionResult> AddNote(string code, [FromBody] AddParcelNoteCommand? body) => Created((body ?? new AddParcelNoteCommand()) with { Code = code });

    [HttpPost("{code}/admin-delete")]
    [RequirePermission("parcels.write")]
    [Guards(Guard.NotMerchant)]
    public Task<IActionResult> AdminDelete(string code, [FromBody] AdminDeleteParcelCommand? body) => Ok((body ?? new AdminDeleteParcelCommand()) with { Code = code });

    [HttpPost("{code}/client/preview")]
    [RequirePermission("parcels.write")]
    [Guards(Guard.NotMerchant)]
    public Task<IActionResult> PreviewClientChange(string code, [FromBody] PreviewParcelClientChangeCommand? body) => Ok((body ?? new PreviewParcelClientChangeCommand()) with { Code = code });

    [HttpPost("{code}/client")]
    [RequirePermission("parcels.write")]
    [Guards(Guard.NotMerchant)]
    public Task<IActionResult> ChangeClient(string code, [FromBody] ChangeParcelClientCommand? body) => Ok((body ?? new ChangeParcelClientCommand()) with { Code = code });

    [HttpPatch("{code}/destination")]
    [RequirePermission("parcels.write")]
    [Guards(Guard.NotMerchant)]
    public Task<IActionResult> ChangeDestination(string code, [FromBody] ChangeParcelDestinationCommand? body) => Ok((body ?? new ChangeParcelDestinationCommand()) with { Code = code });

    [HttpPost("{code}/exchange")]
    [RequirePermission("parcels.write")]
    public Task<IActionResult> RequestExchange(string code, [FromBody] RequestParcelExchangeCommand? body) => Created((body ?? new RequestParcelExchangeCommand()) with { Code = code });
}
