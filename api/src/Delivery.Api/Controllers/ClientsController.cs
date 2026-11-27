using System.Text.Json.Nodes;
using Delivery.Api.Auth;
using Delivery.Application.Clients.Commands;
using Delivery.Application.Clients.Queries;
using Delivery.Application.Common.Models;
using Delivery.Application.Users.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

[Route("clients")]
[Guards(Guard.NotMerchant)]
public sealed class ClientsController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [RequirePermission("directory.read")]
    public Task<IActionResult> List([FromQuery] ListQueryParams query, [FromQuery] string[]? agencyId, [FromQuery] string? validated) =>
        Ok(new GetClientsQuery(query.ToQuery(), MultiValue.AsList(agencyId), validated));

    [HttpGet("export")]
    [RequirePermission("directory.read")]
    public Task<IActionResult> Export([FromQuery] ListQueryParams query, [FromQuery] string[]? agencyId, [FromQuery] string? validated) =>
        FileOf(new ExportClientsQuery(new GetClientsQuery(query.ToQuery(), MultiValue.AsList(agencyId), validated)));

    [HttpGet("{id}")]
    [RequirePermission("directory.read")]
    public Task<IActionResult> Detail(string id) => Ok(new GetClientQuery(id));

    [HttpPost]
    [RequirePermission("directory.write")]
    public Task<IActionResult> Create([FromBody] JsonNode? body) => Created(new CreateClientCommand(body));

    [HttpPatch("{id}")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> Update(string id, [FromBody] JsonNode? body) => Ok(new UpdateClientCommand(id, body));

    [HttpPatch("{id}/vat-rate")]
    [RequirePermission("finance.validate")]
    public Task<IActionResult> SetVatRate(string id, [FromBody] SetClientVatRateCommand? body) => Ok((body ?? new SetClientVatRateCommand()) with { Id = id });

    [HttpPatch("{id}/internal")]
    [RequirePermission("finance.validate")]
    public Task<IActionResult> SetInternal(string id, [FromBody] SetClientInternalCommand? body) => Ok((body ?? new SetClientInternalCommand()) with { Id = id });

    [HttpPatch("{id}/pickup-terms")]
    [RequirePermission("finance.validate")]
    public Task<IActionResult> SetPickupTerms(string id, [FromBody] SetClientPickupTermsCommand? body) => Ok((body ?? new SetClientPickupTermsCommand()) with { Id = id });

    [HttpPatch("{id}/active")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> SetActive(string id, [FromBody] SetClientActiveCommand? body) => Ok((body ?? new SetClientActiveCommand()) with { Id = id });

    [HttpPatch("{id}/validation")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> SetValidated(string id, [FromBody] SetClientValidatedCommand? body) => Ok((body ?? new SetClientValidatedCommand()) with { Id = id });

    [HttpPost("{id}/validate")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> Validate(string id, [FromBody] ValidateClientCommand? body) => Created((body ?? new ValidateClientCommand()) with { Id = id });

    [HttpPost("{id}/reject")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> Reject(string id, [FromBody] RejectClientCommand? body) => Created((body ?? new RejectClientCommand()) with { Id = id });

    [HttpPost("{id}/account")]
    [RequirePermission("accounts.write")]
    public Task<IActionResult> CreateAccount(string id, [FromBody] CreateUserCommand? body) => Created(new CreateClientAccountCommand(id, body ?? new CreateUserCommand()));

    [HttpDelete("{id}")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> Remove(string id) => Ok(new RemoveClientCommand(id));

    [HttpPost("{id}/discounts")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> AddDiscount(string id, [FromBody] AddClientDiscountCommand? body) => Created((body ?? new AddClientDiscountCommand()) with { ClientId = id });

    [HttpPatch("{id}/discounts/{discountId}")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> UpdateDiscount(string id, string discountId, [FromBody] UpdateClientDiscountCommand? body) =>
        Ok((body ?? new UpdateClientDiscountCommand()) with { ClientId = id, DiscountId = discountId });

    [HttpDelete("{id}/discounts/{discountId}")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> RemoveDiscount(string id, string discountId) => Ok(new RemoveClientDiscountCommand(id, discountId));

    [HttpPost("{id}/pickup-addresses")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> AddPickupAddress(string id, [FromBody] AddClientPickupAddressCommand? body) => Created((body ?? new AddClientPickupAddressCommand()) with { ClientId = id });

    [HttpDelete("{id}/pickup-addresses/{addressId}")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> RemovePickupAddress(string id, string addressId) => Ok(new RemoveClientPickupAddressCommand(id, addressId));

    [HttpPost("{id}/documents")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> AddDocument(string id, [FromBody] AddClientDocumentCommand? body) => Created((body ?? new AddClientDocumentCommand()) with { ClientId = id });

    [HttpGet("{id}/documents/{documentId}")]
    [RequirePermission("directory.read")]
    public Task<IActionResult> ReadDocument(string id, string documentId) => FileOf(new GetClientDocumentQuery(id, documentId));

    [HttpDelete("{id}/documents/{documentId}")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> RemoveDocument(string id, string documentId) => Ok(new RemoveClientDocumentCommand(id, documentId));
}
