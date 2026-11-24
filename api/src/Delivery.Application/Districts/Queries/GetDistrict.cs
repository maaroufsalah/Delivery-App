using AutoMapper;
using AutoMapper.QueryableExtensions;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Districts.Queries;

/// <summary>`GET /districts/:id`.</summary>
public sealed record GetDistrictQuery(string Id) : IRequest<DistrictListItemDto>;

internal sealed class GetDistrictHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<GetDistrictQuery, DistrictListItemDto>
{
    public Task<DistrictListItemDto> Handle(GetDistrictQuery request, CancellationToken cancellationToken)
    {
        caller.AssertCanSeeInternalPricing("Quartiers");
        return DistrictRows.LoadAsync(db, mapper, request.Id, cancellationToken);
    }
}
