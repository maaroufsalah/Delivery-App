using Delivery.Api.Auth;
using Delivery.Application.Identity.Commands;
using Delivery.Application.Identity.Queries;
using Delivery.Application.Public.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

[Route("public")]
[Guards(Guard.PublicRateLimit)]
public sealed class PublicController(ISender sender) : ApiControllerBase(sender)
{
    [Public]
    [HttpGet("company")]
    public Task<IActionResult> Company() => Ok(new GetPublicCompanyQuery());

    [Public]
    [HttpGet("logo/{fileName}")]
    public Task<IActionResult> Logo(string fileName) => FileOf(new GetPublicLogoQuery(fileName));

    [Public]
    [HttpGet("rates")]
    public Task<IActionResult> Rates() => Ok(new GetPublicRatesQuery());

    [Public]
    [HttpGet("mobile-config")]
    public Task<IActionResult> MobileConfig() => Ok(new GetMobileConfigQuery());

    [Public]
    [HttpGet("cities")]
    public Task<IActionResult> Cities() => Ok(new GetPublicCitiesQuery());

    [Public]
    [HttpGet("track/{code}")]
    public Task<IActionResult> Track(string code) => Ok(new TrackParcelQuery(code));

    [Public]
    [HttpPost("register")]
    public Task<IActionResult> Register([FromBody] RegisterCommand? body) => Created(body ?? new RegisterCommand());
}

[Route("health")]
public sealed class HealthController(ISender sender) : ApiControllerBase(sender)
{
    [Public]
    [HttpGet]
    public async Task<IActionResult> Check()
    {
        var health = await Sender.Send(new GetHealthQuery(), HttpContext.RequestAborted);
        return StatusCode(health.Database == "down" ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status200OK, health);
    }
}
