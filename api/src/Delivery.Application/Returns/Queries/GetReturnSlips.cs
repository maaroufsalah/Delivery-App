using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.CourierApp;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using Delivery.Domain.Pricing;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Returns;

/// <summary>`GET /return-slips`.</summary>
public sealed record GetReturnSlipsQuery(SlipListFilter Filter) : IRequest<Paginated<ReturnSlipListItemDto>>;

internal sealed class GetReturnSlipsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetReturnSlipsQuery, Paginated<ReturnSlipListItemDto>>
{
    public async Task<Paginated<ReturnSlipListItemDto>> Handle(GetReturnSlipsQuery request, CancellationToken cancellationToken)
    {
        var where = SlipLists.Filter(db, caller, request.Filter);
        var paging = request.Filter.Paging;
        var total = await where.CountAsync(cancellationToken);
        var items = await Slips.Project(where.Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize)).ToListAsync(cancellationToken);
        return new Paginated<ReturnSlipListItemDto>(items, total, paging.Page, paging.PageSize);
    }
}
