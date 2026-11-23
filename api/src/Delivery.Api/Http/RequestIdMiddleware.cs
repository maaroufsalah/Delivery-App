using System.Text.RegularExpressions;
using Serilog.Context;

namespace Delivery.Api.Http;

/// <summary>
/// Un identifiant par requête, repris de `x-request-id` s'il est sûr, sinon
/// tiré au hasard ; renvoyé au client et porté par chaque ligne de log.
/// </summary>
public sealed class RequestIdMiddleware(RequestDelegate next)
{
    public const string Header = "x-request-id";

    private static readonly Regex SafeId = new("^[A-Za-z0-9-]{8,64}$", RegexOptions.Compiled);

    public async Task InvokeAsync(HttpContext context)
    {
        var given = context.Request.Headers[Header].FirstOrDefault();
        var id = given is not null && SafeId.IsMatch(given) ? given : Guid.NewGuid().ToString();
        context.TraceIdentifier = id;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[Header] = id;
            return Task.CompletedTask;
        });
        using (LogContext.PushProperty("requestId", id))
        {
            await next(context);
        }
    }
}
