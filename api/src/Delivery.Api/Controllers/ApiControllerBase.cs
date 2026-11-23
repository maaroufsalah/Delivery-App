using Delivery.Application.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

/// <summary>
/// Contrôleurs minces : ils envoient la requête et traduisent la réponse.
/// Un `@Post` de Nest répond 201 par défaut, d'où <see cref="Created{T}"/>.
/// </summary>
[ApiController]
public abstract class ApiControllerBase(ISender sender) : ControllerBase
{
    protected ISender Sender { get; } = sender;

    protected async Task<IActionResult> Ok<T>(IRequest<T> request) => base.Ok(await Sender.Send(request, HttpContext.RequestAborted));

    protected async Task<IActionResult> Created<T>(IRequest<T> request) => StatusCode(StatusCodes.Status201Created, await Sender.Send(request, HttpContext.RequestAborted));

    protected async Task<IActionResult> FileOf(IRequest<FileContent> request) => FileResult(await Sender.Send(request, HttpContext.RequestAborted));

    protected IActionResult FileResult(FileContent file)
    {
        var disposition = file.Inline ? "inline" : "attachment";
        Response.Headers.ContentDisposition = $"{disposition}; filename=\"{file.FileName}\"";
        if (file.CacheControl is not null) Response.Headers.CacheControl = file.CacheControl;
        return File(file.Bytes, file.ContentType);
    }
}
