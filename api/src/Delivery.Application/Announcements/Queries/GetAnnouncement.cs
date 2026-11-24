using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Announcements;

/// <summary>`detail` : un brouillon n'est lisible que de la rédaction.</summary>
public sealed record GetAnnouncementQuery(string Id) : IRequest<AnnouncementDetailDto>;

internal sealed class GetAnnouncementHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetAnnouncementQuery, AnnouncementDetailDto>
{
    public async Task<AnnouncementDetailDto> Handle(GetAnnouncementQuery input, CancellationToken cancellationToken)
    {
        var row = await AnnouncementRows.Readable(db, caller, clock.UtcNow).Where(a => a.Id == input.Id).Select(AnnouncementRows.Detail).FirstOrDefaultAsync(cancellationToken);
        if (row is null || (!row.IsPublished && !AnnouncementRows.CanWrite(caller))) throw BusinessException.NotFound("Actualité introuvable.");
        return row;
    }
}
