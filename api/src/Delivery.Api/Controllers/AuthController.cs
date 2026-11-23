using Delivery.Api.Auth;
using Delivery.Application.Identity.Commands;
using Delivery.Application.Identity.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

public sealed record RefreshBody(string? RefreshToken);

[Route("auth")]
public sealed class AuthController(ISender sender, IConfiguration configuration) : ApiControllerBase(sender)
{
    [Public]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand? body)
    {
        var result = await Sender.Send(body ?? new LoginCommand(), HttpContext.RequestAborted);
        AuthCookies.Write(Response, configuration, result.Session);
        return Ok(result.ToResponse());
    }

    [Public]
    [Guards(Guard.PublicRateLimit)]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommand? body) =>
        Ok(await Sender.Send(body ?? new ForgotPasswordCommand(), HttpContext.RequestAborted));

    [Public]
    [HttpPost("impersonate")]
    public async Task<IActionResult> Impersonate([FromBody] ImpersonateCommand? body)
    {
        var result = await Sender.Send(body ?? new ImpersonateCommand(), HttpContext.RequestAborted);
        AuthCookies.Write(Response, configuration, result.Session);
        return Ok(result.ToResponse());
    }

    [Public]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshBody? body)
    {
        var result = await Sender.Send(new RefreshCommand(body?.RefreshToken, AuthCookies.Read(Request, AuthCookies.Refresh)), HttpContext.RequestAborted);
        AuthCookies.Write(Response, configuration, result.Session);
        return Ok(result.ToResponse());
    }

    [Public]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshBody? body)
    {
        var access = AuthCookies.Read(Request, AuthCookies.Access) ?? AuthCookies.Bearer(Request);
        await Sender.Send(new LogoutCommand(access, AuthCookies.Read(Request, AuthCookies.Refresh), body?.RefreshToken), HttpContext.RequestAborted);
        AuthCookies.Clear(Response, configuration);
        return NoContent();
    }

    [HttpGet("me")]
    public Task<IActionResult> Me() => Ok(new GetProfileQuery());

    [HttpDelete("me")]
    public async Task<IActionResult> DeleteMe()
    {
        var result = await Sender.Send(new DeleteOwnAccountCommand(), HttpContext.RequestAborted);
        AuthCookies.Clear(Response, configuration);
        return Ok(result);
    }
}
