using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Settings;
using Delivery.Application.Settings.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using Delivery.Domain.Pricing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Delivery.Application.Public.Queries;

/// <summary>`GET /public/cities` : villes ouvertes à l'inscription.</summary>
public sealed record GetPublicCitiesQuery : IRequest<IReadOnlyList<PublicCityDto>>;

public sealed record PublicCityDto(string Id, string Name);

internal sealed class GetPublicCitiesHandler(IAppDbContext db) : IRequestHandler<GetPublicCitiesQuery, IReadOnlyList<PublicCityDto>>
{
    public async Task<IReadOnlyList<PublicCityDto>> Handle(GetPublicCitiesQuery request, CancellationToken cancellationToken) =>
        await db.Cities.AsNoTracking().Where(c => c.IsActive && c.DeletedAt == null).OrderBy(c => c.Name)
            .Select(c => new PublicCityDto(c.Id, c.Name)).ToListAsync(cancellationToken);
}
