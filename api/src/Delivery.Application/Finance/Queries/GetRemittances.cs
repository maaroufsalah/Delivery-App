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
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Finance;

/// <summary>`GET /remittances`.</summary>
public sealed record GetRemittancesQuery(RemittanceListFilter Filter) : IRequest<Paginated<RemittanceListItemDto>>;

internal sealed class GetRemittancesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetRemittancesQuery, Paginated<RemittanceListItemDto>>
{
    public async Task<Paginated<RemittanceListItemDto>> Handle(GetRemittancesQuery request, CancellationToken cancellationToken)
    {
        var where = RemittanceLists.Filter(db, caller, request.Filter);
        var paging = request.Filter.Paging;
        var total = await where.CountAsync(cancellationToken);
        var items = await Remittances.ListAsync(where.Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize), cancellationToken);
        return new Paginated<RemittanceListItemDto>(items, total, paging.Page, paging.PageSize);
    }
}
