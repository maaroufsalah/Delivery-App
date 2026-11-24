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

/// <summary>`GET /zones/rates` : matrice des tarifs entre zones.</summary>
public sealed record GetZoneRatesQuery : IRequest<ZoneRateMatrixDto>;

internal sealed class GetZoneRatesHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<GetZoneRatesQuery, ZoneRateMatrixDto>
{
    public async Task<ZoneRateMatrixDto> Handle(GetZoneRatesQuery request, CancellationToken cancellationToken)
    {
        caller.AssertCanSeeInternalPricing("Zones");
        var zones = await db.Zones.AsNoTracking().Where(z => z.DeletedAt == null).OrderBy(z => z.Name)
            .Select(z => new ZoneRefDto(z.Id, z.Name)).ToListAsync(cancellationToken);
        var rates = await mapper.ProjectTo<ZoneRateRow>(db.ZoneRates.AsNoTracking().OrderBy(r => r.CreatedAt)).ToListAsync(cancellationToken);
        return new ZoneRateMatrixDto(zones, rates.Select(r => r.ToDto()).ToList());
    }
}
