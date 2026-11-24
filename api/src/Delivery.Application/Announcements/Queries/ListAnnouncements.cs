using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Announcements;

/// <summary>`announcementFiltersSchema` : toutes les actualités, brouillons compris (rédaction).</summary>
public sealed record ListAnnouncementsQuery(string? Search, string? Audience, string? Published, string? Page, string? PageSize) : IRequest<Paginated<AnnouncementListItemDto>>;

internal sealed class ListAnnouncementsHandler(IAppDbContext db) : IRequestHandler<ListAnnouncementsQuery, Paginated<AnnouncementListItemDto>>
{
    public async Task<Paginated<AnnouncementListItemDto>> Handle(ListAnnouncementsQuery input, CancellationToken cancellationToken)
    {
        var issues = new List<object>();
        var page = ListQueryParams.Coerce(input.Page, 1, 1, null, "page", issues);
        var pageSize = ListQueryParams.Coerce(input.PageSize, 20, 5, 200, "pageSize", issues);
        if (input.Audience is not null && !AnnouncementRows.Audiences.Contains(input.Audience))
        {
            issues.Add(new { path = "audience", message = Common.Validation.Zod.InvalidOption(AnnouncementRows.Audiences) });
        }
        if (input.Published is not null and not ("true" or "false")) issues.Add(new { path = "published", message = "Invalid option: expected one of \"true\"|\"false\"" });
        if (issues.Count > 0) throw BusinessException.Validation("Saisie invalide.", new { issues });

        var rows = db.Announcements.AsNoTracking().Where(a => a.DeletedAt == null);
        if (input.Audience is not null)
        {
            var audience = Enum.Parse<AnnouncementAudience>(Money.FromWire(input.Audience));
            rows = rows.Where(a => a.Audience == audience);
        }
        if (input.Published is not null)
        {
            var published = input.Published == "true";
            rows = rows.Where(a => a.IsPublished == published);
        }
        var needle = input.Search?.Trim();
        if (!string.IsNullOrEmpty(needle))
        {
            var pattern = Like.Contains(needle);
            rows = rows.Where(a => EF.Functions.ILike(a.Title, pattern, "\\") || (a.Intro != null && EF.Functions.ILike(a.Intro, pattern, "\\")) || EF.Functions.ILike(a.Body, pattern, "\\"));
        }
        var total = await rows.CountAsync(cancellationToken);
        var items = await rows.OrderByDescending(a => a.PublishedAt).ThenByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(AnnouncementRows.Item).ToListAsync(cancellationToken);
        return new Paginated<AnnouncementListItemDto>(items, total, page, pageSize);
    }
}
