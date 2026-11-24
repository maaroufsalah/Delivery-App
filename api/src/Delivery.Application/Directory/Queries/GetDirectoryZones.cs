using System.Text.Json.Serialization;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Directory.Queries;

/// <summary>`GET /directory/zones` : les tarifs ne sortent que pour le personnel.</summary>
public sealed record GetDirectoryZonesQuery : IRequest<IReadOnlyList<DirectoryZoneDto>>;

public sealed record DirectoryZoneDto(
    string Id,
    string Name,
    int DeliveryDays,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? BaseRate,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? SameCityRate);

internal sealed class GetDirectoryZonesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetDirectoryZonesQuery, IReadOnlyList<DirectoryZoneDto>>
{
    public async Task<IReadOnlyList<DirectoryZoneDto>> Handle(GetDirectoryZonesQuery request, CancellationToken cancellationToken)
    {
        var internalView = caller.CanSeeInternalPricing();
        var rows = await db.Zones.AsNoTracking()
            .Where(z => z.DeletedAt == null && (internalView || z.IsActive))
            .OrderBy(z => z.Name)
            .Select(z => new { z.Id, z.Name, z.DeliveryDays, z.BaseRate, z.SameCityRate })
            .ToListAsync(cancellationToken);
        return rows.Select(z => new DirectoryZoneDto(
            z.Id, z.Name, z.DeliveryDays,
            internalView ? Money.ToCents(z.BaseRate) : null,
            internalView ? Money.ToCents(z.SameCityRate) : null)).ToList();
    }
}
