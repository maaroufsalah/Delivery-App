using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Claims;

/// <summary>`list` : les réclamations de sa portée, les plus récentes d'abord.</summary>
public sealed record ListClaimsQuery(ClaimListFilter Filter) : IRequest<Paginated<ClaimListItemDto>>;

internal sealed class ListClaimsHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<ListClaimsQuery, Paginated<ClaimListItemDto>>
{
    public async Task<Paginated<ClaimListItemDto>> Handle(ListClaimsQuery input, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var (rows, page, pageSize) = await ClaimRows.FilterAsync(db, caller, input.Filter, now, cancellationToken);
        var total = await rows.CountAsync(cancellationToken);
        var items = await rows.OrderByDescending(c => c.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).Select(ClaimRows.Row).ToListAsync(cancellationToken);
        return new Paginated<ClaimListItemDto>(items.Select(r => ClaimRows.Item(r, now)).ToList(), total, page, pageSize);
    }
}
