using System.Text.Json;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Persistence;
using Delivery.Application.Common.Validation;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Parcels.Import;

/// <summary>`GET /parcels/imports` : l'historique des imports (le vendeur voit les siens).</summary>
public sealed record GetParcelImportsQuery(ListQuery Paging, string? ClientId) : IRequest<Paginated<ParcelImportListItemDto>>;

internal sealed class GetParcelImportsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetParcelImportsQuery, Paginated<ParcelImportListItemDto>>
{
    public async Task<Paginated<ParcelImportListItemDto>> Handle(GetParcelImportsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Paging.Page, 1);
        var pageSize = Math.Min(Math.Max(request.Paging.PageSize, 1), 100);
        var where = db.ParcelImports.AsNoTracking();
        if (caller.ClientId is { } own) where = where.Where(i => i.ClientId == own);
        else if (!string.IsNullOrEmpty(request.ClientId)) where = where.Where(i => i.ClientId == request.ClientId);
        var total = await where.CountAsync(cancellationToken);
        var rows = await where.OrderByDescending(i => i.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(i => new { i.Id, i.Filename, i.ClientId, ClientName = i.Client.Name, i.AuthorName, i.Mode, i.RowCount, i.OkCount, i.KoCount, i.CreatedAt })
            .ToListAsync(cancellationToken);
        var items = rows.Select(i => new ParcelImportListItemDto(i.Id, i.Filename, i.ClientId, i.ClientName, i.AuthorName,
            i.Mode == ParcelImportMode.VALID_ONLY ? "valid-only" : "all-or-nothing", i.RowCount, i.OkCount, i.KoCount, i.CreatedAt)).ToList();
        return new Paginated<ParcelImportListItemDto>(items, total, page, pageSize);
    }
}
