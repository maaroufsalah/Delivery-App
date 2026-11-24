using AutoMapper;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Zones.Dtos;
using Delivery.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Zones.Queries;

/// <summary>`GET /zones/:id`.</summary>
public sealed record GetZoneQuery(string Id) : IRequest<ZoneListItemDto>;

internal sealed class GetZoneHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<GetZoneQuery, ZoneListItemDto>
{
    public Task<ZoneListItemDto> Handle(GetZoneQuery request, CancellationToken cancellationToken)
    {
        caller.AssertCanSeeInternalPricing("Zones");
        return ZoneRows.LoadAsync(db, mapper, request.Id, cancellationToken);
    }
}
