using System.Text.Json.Serialization;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Directory.Queries;

/// <summary>`GET /directory/agencies` : l'agent et le vendeur ne voient que leur agence.</summary>
public sealed record GetDirectoryAgenciesQuery : IRequest<IReadOnlyList<DirectoryAgencyDto>>;

public sealed record DirectoryAgencyDto(string Id, string Name, string CityId, string Phone, string Address);

internal sealed class GetDirectoryAgenciesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetDirectoryAgenciesQuery, IReadOnlyList<DirectoryAgencyDto>>
{
    public async Task<IReadOnlyList<DirectoryAgencyDto>> Handle(GetDirectoryAgenciesQuery request, CancellationToken cancellationToken)
    {
        var query = db.Agencies.AsNoTracking().Where(a => a.DeletedAt == null);
        if (!caller.IsSuperAdmin())
        {
            if (caller.AgencyId is { } agencyId) query = query.Where(a => a.Id == agencyId);
            if (caller.ClientId is not null)
            {
                var pinned = caller.AgencyId ?? Scope.DenyAllId;
                query = query.Where(a => a.Id == pinned);
            }
        }
        return await query.OrderBy(a => a.Name)
            .Select(a => new DirectoryAgencyDto(a.Id, a.Name, a.CityId, a.Phone ?? string.Empty, a.Address ?? string.Empty))
            .ToListAsync(cancellationToken);
    }
}
