using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Application.PettyCash;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Finance;

/// <summary>`GET /cod-payouts`.</summary>
public sealed record GetCodPayoutsQuery(CodPayoutListFilter Filter) : IRequest<Paginated<CodPayoutListItemDto>>;

internal sealed class GetCodPayoutsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetCodPayoutsQuery, Paginated<CodPayoutListItemDto>>
{
    public async Task<Paginated<CodPayoutListItemDto>> Handle(GetCodPayoutsQuery request, CancellationToken cancellationToken)
    {
        var where = CodPayoutLists.Filter(db, caller, request.Filter);
        var paging = request.Filter.Paging;
        var total = await where.CountAsync(cancellationToken);
        var items = (await CodPayouts.Project(where.Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize)).ToListAsync(cancellationToken)).Select(CodPayouts.Wire).ToList();
        return new Paginated<CodPayoutListItemDto>(items, total, paging.Page, paging.PageSize);
    }
}
