using Delivery.Api.Auth;
using Delivery.Application.Cities.Commands;
using Delivery.Application.Cities.Queries;
using Delivery.Application.Common.Models;
using Delivery.Application.Agencies.Commands;
using Delivery.Application.Agencies.Queries;
using Delivery.Application.DeliveryTypes.Commands;
using Delivery.Application.DeliveryTypes.Queries;
using Delivery.Application.Districts.Commands;
using Delivery.Application.Districts.Queries;
using Delivery.Application.Directory.Queries;
using Delivery.Application.Pricing.Commands;
using Delivery.Application.Pricing.Queries;
using Delivery.Application.Zones.Commands;
using Delivery.Application.Zones.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

[Route("cities")]
[Guards(Guard.NotMerchant)]
public sealed class CitiesController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [RequirePermission("directory.read")]
    public Task<IActionResult> List([FromQuery] ListQueryParams query, [FromQuery] string[]? zoneId) =>
        Ok(new GetCitiesQuery(query.ToQuery(), MultiValue.AsList(zoneId)));

    [HttpGet("export")]
    [RequirePermission("directory.read")]
    public Task<IActionResult> Export([FromQuery] ListQueryParams query, [FromQuery] string[]? zoneId) =>
        FileOf(new ExportCitiesQuery(new GetCitiesQuery(query.ToQuery(), MultiValue.AsList(zoneId))));

    [HttpGet("{id}")]
    [RequirePermission("directory.read")]
    public Task<IActionResult> Detail(string id) => Ok(new GetCityQuery(id));

    [HttpPost]
    [RequirePermission("directory.write")]
    public Task<IActionResult> Create([FromBody] CreateCityCommand? body) => Created(body ?? new CreateCityCommand());

    [HttpPatch("{id}")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> Update(string id, [FromBody] UpdateCityCommand? body) => Ok((body ?? new UpdateCityCommand()) with { Id = id });

    [HttpPatch("{id}/active")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> SetActive(string id, [FromBody] SetCityActiveCommand? body) => Ok((body ?? new SetCityActiveCommand()) with { Id = id });

    [HttpDelete("{id}")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> Remove(string id) => Ok(new RemoveCityCommand(id));
}

[Route("zones")]
[Guards(Guard.NotMerchant)]
public sealed class ZonesController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [RequirePermission("directory.read")]
    public Task<IActionResult> List([FromQuery] ListQueryParams query) => Ok(new GetZonesQuery(query.ToQuery()));

    [HttpGet("export")]
    [RequirePermission("directory.read")]
    public Task<IActionResult> Export([FromQuery] ListQueryParams query) => FileOf(new ExportZonesQuery(new GetZonesQuery(query.ToQuery())));

    [HttpGet("rates")]
    [RequirePermission("directory.read")]
    public Task<IActionResult> Rates() => Ok(new GetZoneRatesQuery());

    [HttpPost("rates")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> UpsertRate([FromBody] UpsertZoneRateCommand? body) => Created(body ?? new UpsertZoneRateCommand());

    [HttpDelete("rates/{id}")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> RemoveRate(string id) => Ok(new RemoveZoneRateCommand(id));

    [HttpGet("{id}")]
    [RequirePermission("directory.read")]
    public Task<IActionResult> Detail(string id) => Ok(new GetZoneQuery(id));

    [HttpPost]
    [RequirePermission("directory.write")]
    public Task<IActionResult> Create([FromBody] CreateZoneCommand? body) => Created(body ?? new CreateZoneCommand());

    [HttpPatch("{id}")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> Update(string id, [FromBody] UpdateZoneCommand? body) => Ok((body ?? new UpdateZoneCommand()) with { Id = id });

    [HttpPatch("{id}/active")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> SetActive(string id, [FromBody] SetZoneActiveCommand? body) => Ok((body ?? new SetZoneActiveCommand()) with { Id = id });

    [HttpDelete("{id}")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> Remove(string id) => Ok(new RemoveZoneCommand(id));
}

[Route("districts")]
[Guards(Guard.NotMerchant)]
public sealed class DistrictsController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [RequirePermission("directory.read")]
    public Task<IActionResult> List([FromQuery] ListQueryParams query, [FromQuery] string[]? cityId) => Ok(new GetDistrictsQuery(query.ToQuery(), MultiValue.AsList(cityId)));

    [HttpGet("export")]
    [RequirePermission("directory.read")]
    public Task<IActionResult> Export([FromQuery] ListQueryParams query, [FromQuery] string[]? cityId) => FileOf(new ExportDistrictsQuery(new GetDistrictsQuery(query.ToQuery(), MultiValue.AsList(cityId))));

    [HttpGet("{id}")]
    [RequirePermission("directory.read")]
    public Task<IActionResult> Detail(string id) => Ok(new GetDistrictQuery(id));

    [HttpPost]
    [RequirePermission("directory.write")]
    public Task<IActionResult> Create([FromBody] CreateDistrictCommand? body) => Created(body ?? new CreateDistrictCommand());

    [HttpPatch("{id}")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> Update(string id, [FromBody] UpdateDistrictCommand? body) => Ok((body ?? new UpdateDistrictCommand()) with { Id = id });

    [HttpPatch("{id}/active")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> SetActive(string id, [FromBody] SetDistrictActiveCommand? body) => Ok((body ?? new SetDistrictActiveCommand()) with { Id = id });

    [HttpDelete("{id}")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> Remove(string id) => Ok(new RemoveDistrictCommand(id));
}

[Route("delivery-types")]
[Guards(Guard.NotMerchant)]
public sealed class DeliveryTypesController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [RequirePermission("directory.read")]
    public Task<IActionResult> List([FromQuery] ListQueryParams query) => Ok(new GetDeliveryTypesQuery(query.ToQuery()));

    [HttpGet("export")]
    [RequirePermission("directory.read")]
    public Task<IActionResult> Export([FromQuery] ListQueryParams query) => FileOf(new ExportDeliveryTypesQuery(new GetDeliveryTypesQuery(query.ToQuery())));

    [HttpGet("{id}")]
    [RequirePermission("directory.read")]
    public Task<IActionResult> Detail(string id) => Ok(new GetDeliveryTypeQuery(id));

    [HttpPost]
    [RequirePermission("directory.write")]
    public Task<IActionResult> Create([FromBody] CreateDeliveryTypeCommand? body) => Created(body ?? new CreateDeliveryTypeCommand());

    [HttpPatch("{id}")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> Update(string id, [FromBody] UpdateDeliveryTypeCommand? body) => Ok((body ?? new UpdateDeliveryTypeCommand()) with { Id = id });

    [HttpPatch("{id}/active")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> SetActive(string id, [FromBody] SetDeliveryTypeActiveCommand? body) => Ok((body ?? new SetDeliveryTypeActiveCommand()) with { Id = id });

    [HttpDelete("{id}")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> Remove(string id) => Ok(new RemoveDeliveryTypeCommand(id));
}

[Route("agencies")]
public sealed class AgenciesController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [RequirePermission("accounts.read")]
    public Task<IActionResult> List([FromQuery] ListQueryParams query) => Ok(new GetAgenciesQuery(query.ToQuery()));

    [HttpGet("export")]
    [RequirePermission("accounts.read")]
    public Task<IActionResult> Export([FromQuery] ListQueryParams query) => FileOf(new ExportAgenciesQuery(new GetAgenciesQuery(query.ToQuery())));

    [HttpGet("{id}")]
    [RequirePermission("accounts.read")]
    public Task<IActionResult> Detail(string id) => Ok(new GetAgencyQuery(id));

    [HttpPost]
    [RequirePermission("accounts.write")]
    public Task<IActionResult> Create([FromBody] CreateAgencyCommand? body) => Created(body ?? new CreateAgencyCommand());

    [HttpPatch("{id}")]
    [RequirePermission("accounts.write")]
    public Task<IActionResult> Update(string id, [FromBody] UpdateAgencyCommand? body) => Ok((body ?? new UpdateAgencyCommand()) with { Id = id });

    [HttpPatch("{id}/active")]
    [RequirePermission("accounts.write")]
    public Task<IActionResult> SetActive(string id, [FromBody] SetAgencyActiveCommand? body) => Ok((body ?? new SetAgencyActiveCommand()) with { Id = id });

    [HttpDelete("{id}")]
    [RequirePermission("accounts.write")]
    public Task<IActionResult> Remove(string id) => Ok(new RemoveAgencyCommand(id));
}

[Route("pricing-rules")]
[Guards(Guard.NotMerchant)]
public sealed class PricingRulesController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [RequirePermission("directory.read")]
    public Task<IActionResult> List([FromQuery] ListQueryParams query, [FromQuery] string? originCityId, [FromQuery] string? destinationCityId) =>
        Ok(new GetPricingRulesQuery(query.ToQuery(), originCityId, destinationCityId));

    [HttpGet("by-origin")]
    [RequirePermission("directory.read")]
    public Task<IActionResult> ByOrigin([FromQuery] ListQueryParams query, [FromQuery] string? originCityId, [FromQuery] string[]? zoneId) =>
        Ok(new GetPricingByOriginQuery(query.ToQuery(), originCityId, MultiValue.AsList(zoneId)));

    [HttpPut("by-origin/{originCityId}")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> SaveOrigin(string originCityId, [FromBody] SavePricingOriginCommand? body) =>
        Ok((body ?? new SavePricingOriginCommand()) with { OriginCityId = originCityId });

    [HttpGet("by-origin/{originCityId}/export")]
    [RequirePermission("directory.read")]
    public Task<IActionResult> ExportOrigin(string originCityId) => FileOf(new ExportPricingOriginQuery(originCityId));

    [HttpPost("by-origin/{originCityId}/import")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> ImportOrigin(string originCityId, [FromBody] ImportPricingOriginCommand? body) =>
        Created((body ?? new ImportPricingOriginCommand()) with { OriginCityId = originCityId });

    [HttpPost]
    [RequirePermission("directory.write")]
    public Task<IActionResult> Create([FromBody] CreatePricingRuleCommand? body) => Created(body ?? new CreatePricingRuleCommand());

    [HttpPatch("{id}")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> Update(string id, [FromBody] UpdatePricingRuleCommand? body) => Ok((body ?? new UpdatePricingRuleCommand()) with { Id = id });

    [HttpDelete("{id}")]
    [RequirePermission("directory.write")]
    public Task<IActionResult> Remove(string id) => Ok(new RemovePricingRuleCommand(id));
}

[Route("directory")]
public sealed class DirectoryController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet("cities")]
    [RequirePermission("directory.read")]
    public Task<IActionResult> Cities([FromQuery] string? includeInactive) => Ok(new GetDirectoryCitiesQuery(includeInactive == "true"));

    [HttpGet("zones")]
    [RequirePermission("directory.read")]
    public Task<IActionResult> Zones() => Ok(new GetDirectoryZonesQuery());

    [HttpGet("districts")]
    [RequirePermission("directory.read")]
    public Task<IActionResult> Districts([FromQuery] string? cityId) => Ok(new GetDirectoryDistrictsQuery(cityId));

    [HttpGet("clients")]
    [RequirePermission("directory.read")]
    public Task<IActionResult> Clients() => Ok(new GetDirectoryClientsQuery());

    [HttpGet("couriers")]
    [RequirePermission("directory.read")]
    public Task<IActionResult> Couriers() => Ok(new GetDirectoryCouriersQuery());

    [HttpGet("agencies")]
    [RequirePermission("directory.read")]
    public Task<IActionResult> Agencies() => Ok(new GetDirectoryAgenciesQuery());

    [HttpGet("pending-counts")]
    [RequirePermission("directory.read")]
    public Task<IActionResult> PendingCounts() => Ok(new GetPendingCountsQuery());
}
