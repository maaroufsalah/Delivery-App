using System.Text.Json;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Audit;

/// <summary>`GET /audit` (vue technique) et `GET /audit/:entity/:entityId`.</summary>
public sealed record GetAuditLogsQuery(AuditListFilter Filter) : IRequest<Paginated<AuditLogListItemDto>>;

internal sealed class GetAuditLogsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetAuditLogsQuery, Paginated<AuditLogListItemDto>>
{
    public async Task<Paginated<AuditLogListItemDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var filter = request.Filter;
        var where = await AuditRows.FilterAsync(db, caller, filter, cancellationToken);
        var total = await where.CountAsync(cancellationToken);
        var rows = await where.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(cancellationToken);
        return new Paginated<AuditLogListItemDto>(rows.Select(r => AuditRows.Fill(new AuditLogListItemDto(), r)).ToList(), total, filter.Page, filter.PageSize);
    }
}
