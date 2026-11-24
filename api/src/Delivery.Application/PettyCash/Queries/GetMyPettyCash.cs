using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Idempotency;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.PettyCash;

/// <summary>`GET /courier/me/petty-cash` (`pettyCashCourierListQuerySchema`).</summary>
public sealed record GetMyPettyCashQuery(string? Page, string? PageSize) : IRequest<PettyCashCourierPageDto>;

internal sealed class GetMyPettyCashHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetMyPettyCashQuery, PettyCashCourierPageDto>
{
    public async Task<PettyCashCourierPageDto> Handle(GetMyPettyCashQuery request, CancellationToken cancellationToken)
    {
        var paging = new ListQueryParams { Page = request.Page, PageSize = request.PageSize };
        var query = paging.ToQueryWithDefault(50);
        if (query.PageSize > 100) throw BusinessException.Validation("Saisie invalide.", new { issues = new[] { new { path = "pageSize", message = "Too big: expected number to be <=100" } } });
        var where = CourierExpenses.Mine(db, caller);
        var total = await where.CountAsync(cancellationToken);
        var items = await PettyCashMovements.ProjectAsync(db, where.OrderByDescending(m => m.OccurredAt).ThenByDescending(m => m.CreatedAt).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize),
            cancellationToken);
        var drafts = await where.CountAsync(m => m.Status == PettyCashStatus.DRAFT, cancellationToken);
        return new PettyCashCourierPageDto(items, total, query.Page, query.PageSize, drafts, await PettyCashMovements.CategoriesAsync(db, cancellationToken));
    }
}
