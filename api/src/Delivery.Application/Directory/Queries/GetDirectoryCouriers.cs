using System.Text.Json.Serialization;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Directory.Queries;

/// <summary>`GET /directory/couriers` : vide pour un vendeur.</summary>
public sealed record GetDirectoryCouriersQuery : IRequest<IReadOnlyList<DirectoryCourierDto>>;

public sealed record DirectoryCourierDto(string Id, string FirstName, string LastName, string Phone, string? CityId, string VehicleType, bool IsActive, int CityCount);

internal sealed class GetDirectoryCouriersHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetDirectoryCouriersQuery, IReadOnlyList<DirectoryCourierDto>>
{
    public async Task<IReadOnlyList<DirectoryCourierDto>> Handle(GetDirectoryCouriersQuery request, CancellationToken cancellationToken)
    {
        if (caller.ClientId is not null && !caller.IsSuperAdmin()) return [];
        var rows = await db.Couriers.AsNoTracking().Where(c => c.DeletedAt == null)
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .Select(c => new { c.Id, c.FirstName, c.LastName, c.Phone, c.CityId, c.VehicleType, c.IsActive, Count = c.Cities.Count })
            .ToListAsync(cancellationToken);
        return rows.Select(c => new DirectoryCourierDto(c.Id, c.FirstName, c.LastName, c.Phone, c.CityId, Money.ToWire(c.VehicleType), c.IsActive, c.Count)).ToList();
    }
}
