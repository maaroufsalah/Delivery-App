using System.Text.Json;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Invoices;

/// <summary>`GET /transfer-orders` : les OV déjà générés, consultation seule, recherche par numéro, le plus récent d'abord.</summary>
public sealed record GetTransferOrdersQuery(ListQuery Paging) : IRequest<Paginated<TransferOrderListItemDto>>;

internal sealed class GetTransferOrdersHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetTransferOrdersQuery, Paginated<TransferOrderListItemDto>>
{
    public async Task<Paginated<TransferOrderListItemDto>> Handle(GetTransferOrdersQuery request, CancellationToken cancellationToken)
    {
        var where = db.TransferOrders.AsNoTracking();
        if (!caller.IsSuperAdmin()) { var agency = caller.AgencyId ?? Scope.DenyAllId; where = where.Where(o => o.AgencyId == agency); }
        if (request.Paging.Search is { } search) { var pattern = Like.Contains(search); where = where.Where(o => EF.Functions.ILike(o.Code, pattern)); }
        var paging = request.Paging;
        var total = await where.CountAsync(cancellationToken);
        var rows = await where.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Code).Skip(paging.Skip).Take(paging.PageSize)
            .Select(o => new { o.Code, o.CreatedAt, o.Lines, o.TotalDue, CreatedBy = o.CreatedBy != null ? o.CreatedBy.FirstName + " " + o.CreatedBy.LastName : null })
            .ToListAsync(cancellationToken);
        var lines = rows.ToDictionary(r => r.Code, r => JsonSerializer.Deserialize<List<TransferLineDto>>(r.Lines, JsonSerializerOptions.Web) ?? []);
        var clientIds = lines.Values.SelectMany(l => l.Select(x => x.ClientId)).Distinct().ToList();
        HashSet<string> pro = clientIds.Count == 0 ? [] : (await db.Clients.AsNoTracking().Where(c => clientIds.Contains(c.Id) && c.IsPro).Select(c => c.Id).ToListAsync(cancellationToken)).ToHashSet();
        var items = rows.Select(r =>
        {
            var orderLines = lines[r.Code];
            var kind = orderLines.Count > 0 && orderLines.All(l => pro.Contains(l.ClientId)) ? "pro" : "clients";
            return new TransferOrderListItemDto(r.Code, r.CreatedAt, kind, orderLines.Count, Money.ToCents(r.TotalDue), r.CreatedBy);
        }).ToList();
        return new Paginated<TransferOrderListItemDto>(items, total, paging.Page, paging.PageSize);
    }
}
