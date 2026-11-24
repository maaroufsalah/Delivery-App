using System.Globalization;
using Delivery.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Announcements;

/// <summary>`feed` : les actualités publiées qui concernent ce compte, `Number(limit) || 3`, entre 1 et 50.</summary>
public sealed record GetAnnouncementFeedQuery(string? Limit) : IRequest<IReadOnlyList<AnnouncementDetailDto>>;

internal sealed class GetAnnouncementFeedHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetAnnouncementFeedQuery, IReadOnlyList<AnnouncementDetailDto>>
{
    public async Task<IReadOnlyList<AnnouncementDetailDto>> Handle(GetAnnouncementFeedQuery input, CancellationToken cancellationToken)
    {
        var text = input.Limit?.Trim();
        var limit = string.IsNullOrEmpty(text) || !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) || parsed == 0 || double.IsNaN(parsed) ? 3 : parsed;
        var take = (int)Math.Truncate(Math.Min(Math.Max(limit, 1), 50));
        var now = clock.UtcNow;
        var rows = AnnouncementRows.ForAudience(db.Announcements.AsNoTracking(), caller)
            .Where(a => a.DeletedAt == null && a.IsPublished && a.PublishedAt != null && a.PublishedAt <= now);
        return await rows.OrderByDescending(a => a.PublishedAt).Take(take).Select(AnnouncementRows.Detail).ToListAsync(cancellationToken);
    }
}
