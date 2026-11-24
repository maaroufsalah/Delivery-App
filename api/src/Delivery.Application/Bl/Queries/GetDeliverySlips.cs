using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Bl;

/// <summary>`GET /bl/delivery-slips`.</summary>
public sealed record GetDeliverySlipsQuery(ListQuery Paging, DeliverySlipFilter Filter) : IRequest<Paginated<DeliverySlipListItemDto>>;

public sealed class GetDeliverySlipsValidator : ZodValidator<GetDeliverySlipsQuery>
{
    public GetDeliverySlipsValidator()
    {
        RuleFor(x => x.Filter.Status).OneOf(BlRows.SlipStatuses).OverridePropertyName("status");
    }
}

internal sealed class GetDeliverySlipsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetDeliverySlipsQuery, Paginated<DeliverySlipListItemDto>>
{
    public async Task<Paginated<DeliverySlipListItemDto>> Handle(GetDeliverySlipsQuery request, CancellationToken cancellationToken)
    {
        var where = await BlRows.FilterAsync(db, caller, request.Filter, cancellationToken);
        var total = await where.CountAsync(cancellationToken);
        var ordered = request.Paging.Order == "asc" ? where.OrderBy(s => s.CollectedAt).ThenBy(s => s.Id) : where.OrderByDescending(s => s.CollectedAt).ThenBy(s => s.Id);
        var items = await BlRows.Project(ordered.Skip((request.Paging.Page - 1) * request.Paging.PageSize).Take(request.Paging.PageSize)).ToListAsync(cancellationToken);
        return new Paginated<DeliverySlipListItemDto>(items, total, request.Paging.Page, request.Paging.PageSize);
    }
}
