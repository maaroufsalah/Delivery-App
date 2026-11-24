using AutoMapper;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.DeliveryTypes.Queries;

/// <summary>`GET /delivery-types/:id`.</summary>
public sealed record GetDeliveryTypeQuery(string Id) : IRequest<DeliveryTypeListItemDto>;

internal sealed class GetDeliveryTypeHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<GetDeliveryTypeQuery, DeliveryTypeListItemDto>
{
    public Task<DeliveryTypeListItemDto> Handle(GetDeliveryTypeQuery request, CancellationToken cancellationToken)
    {
        caller.AssertCanSeeInternalPricing("Types de livraison");
        return DeliveryTypeRows.LoadAsync(db, mapper, request.Id, cancellationToken);
    }
}
