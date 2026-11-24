using Delivery.Api.Auth;
using Delivery.Application.Announcements;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

/// <summary>`announcements.controller.ts` : la rédaction écrit, chaque compte lit ce qui le concerne.</summary>
[Route("announcements")]
public sealed class AnnouncementsController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet]
    [RequirePermission("announcements.write")]
    public Task<IActionResult> List([FromQuery] string? search, [FromQuery] string? audience, [FromQuery] string? published, [FromQuery] string? page, [FromQuery] string? pageSize) =>
        Ok(new ListAnnouncementsQuery(search, audience, published, page, pageSize));

    [HttpGet("feed")]
    public Task<IActionResult> Feed([FromQuery] string? limit) => Ok(new GetAnnouncementFeedQuery(limit));

    [HttpGet("{id}")]
    public Task<IActionResult> Detail(string id) => Ok(new GetAnnouncementQuery(id));

    [HttpGet("{id}/image")]
    public Task<IActionResult> Image(string id) => FileOf(new GetAnnouncementImageQuery(id));

    [HttpPost]
    [RequirePermission("announcements.write")]
    public Task<IActionResult> Create([FromBody] CreateAnnouncementCommand? body) => Created(body ?? new CreateAnnouncementCommand());

    [HttpPatch("{id}")]
    [RequirePermission("announcements.write")]
    public Task<IActionResult> Update(string id, [FromBody] UpdateAnnouncementCommand? body) => Ok((body ?? new UpdateAnnouncementCommand()) with { Id = id });

    [HttpDelete("{id}")]
    [RequirePermission("announcements.write")]
    public Task<IActionResult> Remove(string id) => Ok(new DeleteAnnouncementCommand(id));
}
