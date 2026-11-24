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

    /// <summary>`withJobStatus` : 202 quand le geste est parti en tâche de masse, sinon le code habituel du verbe.</summary>
    protected async Task<IActionResult> Accepted<T>(IRequest<T> request, int status = StatusCodes.Status201Created)
    {
        var result = await Sender.Send(request, HttpContext.RequestAborted);
        return StatusCode(result is Application.BulkJobs.BulkJobAcceptedDto ? StatusCodes.Status202Accepted : status, result);
    }

    protected async Task<IActionResult> FileOf(IRequest<FileContent> request) => FileResult(await Sender.Send(request, HttpContext.RequestAborted));

    /// <summary>Un fichier rendu par un `@Post` Nest sans `@HttpCode` : 201, comme toute création.</summary>
    protected async Task<IActionResult> CreatedFile(IRequest<FileContent> request)
    {
        var result = FileResult(await Sender.Send(request, HttpContext.RequestAborted));
        Response.StatusCode = StatusCodes.Status201Created;
        return result;
    }

    protected IActionResult FileResult(FileContent file)
    {
        var disposition = file.Inline ? "inline" : "attachment";
        Response.Headers.ContentDisposition = $"{disposition}; filename=\"{file.FileName}\"";
        if (file.CacheControl is not null) Response.Headers.CacheControl = file.CacheControl;
        return File(file.Bytes, file.ContentType);
    }
}

/// <summary>`BULK_JOBS_HEADER` : `x-bulk-jobs: 1` autorise un geste lourd à partir en tâche de masse.</summary>
public static class BulkJobsHeader
{
    public const string Name = "x-bulk-jobs";
}
