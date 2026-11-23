using Delivery.Api.Auth;
using Delivery.Application.Identity.Commands;
using Delivery.Application.Identity.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

[Route("public")]
[Guards(Guard.PublicRateLimit)]
public sealed class PublicController(ISender sender) : ApiControllerBase(sender)
{
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
