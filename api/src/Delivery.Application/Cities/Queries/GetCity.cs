using AutoMapper;
using AutoMapper.QueryableExtensions;
using Delivery.Application.Cities.Dtos;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Cities.Queries;

/// <summary>`GET /cities/:id`.</summary>
public sealed record GetCityQuery(string Id) : IRequest<CityListItemDto>;

internal sealed class GetCityHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<GetCityQuery, CityListItemDto>
{
    public Task<CityListItemDto> Handle(GetCityQuery request, CancellationToken cancellationToken)
    {
        caller.AssertCanSeeInternalPricing("Villes");
        return CityRows.LoadAsync(db, mapper, request.Id, cancellationToken);
    }
}
