using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Exchanges.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Exchanges.Queries;

/// <summary>`GET /exchanges` : les échanges de ma portée, du plus récent au plus ancien.</summary>
public sealed record GetExchangesQuery(ListQuery Paging, IReadOnlyList<string>? Status) : IRequest<Paginated<ExchangeListItemDto>>;

internal sealed class GetExchangesHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<GetExchangesQuery, Paginated<ExchangeListItemDto>>
{
    public async Task<Paginated<ExchangeListItemDto>> Handle(GetExchangesQuery request, CancellationToken cancellationToken)
    {
        var where = ExchangeRows.Scoped(db.Exchanges.AsNoTracking(), caller);
        if (request.Status is { Count: > 0 } statuses)
        {
            // Une valeur hors énumération fait échouer Prisma (500) : reproduit par Enum.Parse.
            var wanted = statuses.Select(s => Enum.Parse<ExchangeStatus>(Money.FromWire(s))).ToList();
            where = where.Where(x => wanted.Contains(x.Status));
        }
        if (!string.IsNullOrEmpty(request.Paging.Search))
        {
            var pattern = Like.Contains(request.Paging.Search);
            where = where.Where(x => EF.Functions.ILike(x.Code, pattern) || EF.Functions.ILike(x.OriginalParcel.Code, pattern));
        }
        var total = await where.CountAsync(cancellationToken);
        var page = request.Paging;
        var rows = await mapper.ProjectTo<ExchangeRow>(where.OrderByDescending(x => x.RequestedAt).Skip((page.Page - 1) * page.PageSize).Take(page.PageSize)).ToListAsync(cancellationToken);
        return new Paginated<ExchangeListItemDto>(rows.Select(r => ExchangeRows.Fill(new ExchangeListItemDto(), r)).ToList(), total, page.Page, page.PageSize);
    }
}
