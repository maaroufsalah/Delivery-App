using System.Globalization;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Returns;

/// <summary>`GET /return-handovers`.</summary>
public sealed record GetReturnHandoversQuery(HandoverListFilter Filter) : IRequest<Paginated<ReturnHandoverListItemDto>>;

internal sealed class GetReturnHandoversHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetReturnHandoversQuery, Paginated<ReturnHandoverListItemDto>>
{
    public async Task<Paginated<ReturnHandoverListItemDto>> Handle(GetReturnHandoversQuery request, CancellationToken cancellationToken)
    {
        var where = HandoverLists.Filter(db, caller, request.Filter);
        var paging = request.Filter.Paging;
        var total = await where.CountAsync(cancellationToken);
        var items = await Handovers.ListAsync(where.Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize), cancellationToken);
        return new Paginated<ReturnHandoverListItemDto>(items, total, paging.Page, paging.PageSize);
    }
}
