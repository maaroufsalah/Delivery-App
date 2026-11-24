using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Announcements;

/// <summary>`image` : l'illustration, sous les mêmes règles de lecture que l'actualité.</summary>
public sealed record GetAnnouncementImageQuery(string Id) : IRequest<FileContent>;

internal sealed class GetAnnouncementImageHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IFileStorage storage) : IRequestHandler<GetAnnouncementImageQuery, FileContent>
{
    public async Task<FileContent> Handle(GetAnnouncementImageQuery input, CancellationToken cancellationToken)
    {
        var row = await AnnouncementRows.Readable(db, caller, clock.UtcNow).Where(a => a.Id == input.Id).Select(a => new { a.ImageKey, a.ImageName, a.IsPublished })
            .FirstOrDefaultAsync(cancellationToken);
        if (row?.ImageKey is null || (!row.IsPublished && !AnnouncementRows.CanWrite(caller))) throw BusinessException.NotFound("Image introuvable.");
        return new FileContent(await storage.GetAsync(row.ImageKey, cancellationToken), "application/octet-stream", row.ImageName ?? "image", CacheControl: "private, max-age=300");
    }
}
