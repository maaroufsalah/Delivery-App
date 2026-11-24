using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.Warehouse.Stock;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Warehouse.Storage;

/// <summary>`GET /storage-requests` : les demandes d'entreposage, de la plus récente.</summary>
public sealed record GetStorageRequestsQuery(StorageListFilter Filter) : IRequest<Paginated<StorageRequestListItemDto>>;

internal sealed class GetStorageRequestsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetStorageRequestsQuery, Paginated<StorageRequestListItemDto>>
{
    public async Task<Paginated<StorageRequestListItemDto>> Handle(GetStorageRequestsQuery request, CancellationToken cancellationToken)
    {
        var where = StorageRows.Filter(db.StorageRequests.AsNoTracking(), caller, request.Filter);
        var paging = request.Filter.Paging;
        var total = await where.CountAsync(cancellationToken);
        var rows = await StorageRows.Project(where.Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize)).ToListAsync(cancellationToken);
        return new Paginated<StorageRequestListItemDto>(rows.Select(r => StorageRows.Fill(new StorageRequestListItemDto(), r)).ToList(), total, paging.Page, paging.PageSize);
    }
}
