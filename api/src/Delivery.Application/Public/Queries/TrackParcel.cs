using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Delivery.Domain.Parcels;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Public.Queries;

/// <summary>`GET /public/track/:code` : le suivi public, statuts repliés sur leur famille.</summary>
public sealed record TrackParcelQuery(string Code) : IRequest<PublicTrackingDto>;

public sealed record PublicTrackingStepDto(string Group, DateTime At, string? CityName);

public sealed record PublicTrackingDto(string Code, string Group, string DestinationCityName, IReadOnlyList<PublicTrackingStepDto> Steps);

internal sealed class TrackParcelHandler(IAppDbContext db) : IRequestHandler<TrackParcelQuery, PublicTrackingDto>
{
    public async Task<PublicTrackingDto> Handle(TrackParcelQuery request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToLower();
        var parcel = await db.Parcels.AsNoTracking().Where(p => p.Code.ToLower() == code && p.DeletedAt == null)
            .Select(p => new
            {
                p.Code,
                p.StatusId,
                City = p.DestinationCity.Name,
                Events = p.Events.OrderBy(e => e.CreatedAt).Select(e => new { e.StatusId, e.CreatedAt, City = e.City != null ? e.City.Name : null }).ToList(),
            }).FirstOrDefaultAsync(cancellationToken)
            ?? throw BusinessException.NotFound($"Colis introuvable : {request.Code}");
        var steps = new List<PublicTrackingStepDto>();
        foreach (var evt in parcel.Events)
        {
            var group = StatusCatalog.GroupOf(evt.StatusId);
            if (group is null || (steps.Count > 0 && steps[^1].Group == group)) continue;
            steps.Add(new PublicTrackingStepDto(group, evt.CreatedAt, evt.City));
        }
        return new PublicTrackingDto(parcel.Code, StatusCatalog.GroupOf(parcel.StatusId) ?? "initial", parcel.City, steps);
    }
}
