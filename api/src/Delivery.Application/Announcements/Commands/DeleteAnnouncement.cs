using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Announcements;

/// <summary>`remove` : retrait logique, dépubliée ; elle reste relisible en base.</summary>
public sealed record DeleteAnnouncementCommand(string Id) : IRequest<AnnouncementIdDto>;

internal sealed class DeleteAnnouncementHandler(IAppDbContext db, IClock clock) : IRequestHandler<DeleteAnnouncementCommand, AnnouncementIdDto>
{
    public async Task<AnnouncementIdDto> Handle(DeleteAnnouncementCommand input, CancellationToken cancellationToken)
    {
        var row = await db.Announcements.FirstOrDefaultAsync(a => a.Id == input.Id && a.DeletedAt == null, cancellationToken) ?? throw BusinessException.NotFound("Actualité introuvable.");
        row.DeletedAt = clock.UtcNow;
        row.IsPublished = false;
        await db.SaveChangesAsync(cancellationToken);
        return new AnnouncementIdDto(row.Id);
    }
}
