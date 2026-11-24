using Delivery.Api.Auth;
using Delivery.Application.Common.Models;
using Delivery.Application.Couriers.Commands;
using Delivery.Application.Couriers.Queries;
using Delivery.Application.Merchant.Commands;
using Delivery.Application.Merchant.Queries;
using Delivery.Application.Users.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

[Route("couriers")]
[Guards(Guard.NotMerchant)]
public sealed class CouriersController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [RequirePermission("directory.read")]
    public Task<IActionResult> List([FromQuery] ListQueryParams query, [FromQuery] string[]? agencyId, [FromQuery] string[]? cityId, [FromQuery] string? validated) =>
        Ok(new GetCouriersQuery(query.ToQuery(), MultiValue.AsList(agencyId), MultiValue.AsList(cityId), validated));

    [HttpGet("export")]
    [RequirePermission("directory.read")]
    public Task<IActionResult> Export([FromQuery] ListQueryParams query, [FromQuery] string[]? agencyId, [FromQuery] string[]? cityId, [FromQuery] string? validated) =>
        FileOf(new ExportCouriersQuery(new GetCouriersQuery(query.ToQuery(), MultiValue.AsList(agencyId), MultiValue.AsList(cityId), validated)));

    [HttpGet("{id}")]
    [RequirePermission("directory.read")]
    public Task<IActionResult> Detail(string id) => Ok(new GetCourierQuery(id));

    [HttpPost]
    [RequirePermission("directory.write")]
    public Task<IActionResult> Create([FromBody] CreateCourierCommand? body) => Created(body ?? new CreateCourierCommand());

    [HttpPatch("{id}")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> Update(string id, [FromBody] UpdateCourierCommand? body) => Ok((body ?? new UpdateCourierCommand()) with { Id = id });

    [HttpPatch("{id}/active")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> SetActive(string id, [FromBody] SetCourierActiveCommand? body) => Ok((body ?? new SetCourierActiveCommand()) with { Id = id });

    [HttpPatch("{id}/validation")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> SetValidated(string id, [FromBody] SetCourierValidatedCommand? body) => Ok((body ?? new SetCourierValidatedCommand()) with { Id = id });

    [HttpPut("{id}/cities")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> SetCities(string id, [FromBody] SetCourierCitiesCommand? body) => Ok((body ?? new SetCourierCitiesCommand()) with { Id = id });

    [HttpPost("{id}/validate")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> Validate(string id, [FromBody] ValidateCourierCommand? body) => Created((body ?? new ValidateCourierCommand()) with { Id = id });

    [HttpPost("{id}/reject")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> Reject(string id, [FromBody] RejectCourierCommand? body) => Created((body ?? new RejectCourierCommand()) with { Id = id });

    [HttpPost("{id}/account")]
    [RequirePermission("accounts.write")]
    public Task<IActionResult> CreateAccount(string id, [FromBody] CreateUserCommand? body) => Created(new CreateCourierAccountCommand(id, body ?? new CreateUserCommand()));

    [HttpDelete("{id}")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> Remove(string id) => Ok(new RemoveCourierCommand(id));
}

[Route("merchant/me")]
[Guards(Guard.Merchant)]
public sealed class MerchantController(ISender sender) : ApiControllerBase(sender)
{
    [HttpPost("pickups")]
    [RequirePermission("notes.request")]
    public Task<IActionResult> RequestPickup([FromBody] Application.DeliveryNotes.RequestMerchantNoteCommand? body) =>
        Ok((body ?? new Application.DeliveryNotes.RequestMerchantNoteCommand()) with { Kind = Domain.Enums.DeliveryNoteType.PICKUP });

    [HttpPost("dropoffs")]
    [RequirePermission("notes.request")]
    public Task<IActionResult> RequestDropoff([FromBody] Application.DeliveryNotes.RequestMerchantNoteCommand? body) =>
        Ok((body ?? new Application.DeliveryNotes.RequestMerchantNoteCommand()) with { Kind = Domain.Enums.DeliveryNoteType.DROPOFF });

    [HttpDelete("notes/{code}")]
    [RequirePermission("notes.request")]
    public Task<IActionResult> CancelNote(string code) => Ok(new Application.DeliveryNotes.CancelMerchantNoteCommand(code));

    [HttpGet("summary")]
    [RequirePermission("parcels.read")]
    public Task<IActionResult> Summary() => Ok(new GetMerchantSummaryQuery());

    [HttpGet("company")]
    [RequirePermission("directory.read")]
    public Task<IActionResult> Company() => Ok(new GetMerchantCompanyQuery());

    [HttpPatch("company")]
    public Task<IActionResult> UpdateCompany([FromBody] UpdateMerchantCompanyCommand? body) => Ok(body ?? new UpdateMerchantCompanyCommand());
}
