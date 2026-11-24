using System.Text.Json.Serialization;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Directory.Queries;

/// <summary>`GET /directory/cities` : la liste légère des listes déroulantes.</summary>
public sealed record GetDirectoryCitiesQuery(bool IncludeInactive) : IRequest<IReadOnlyList<DirectoryCityDto>>;

public sealed record DirectoryCityDto(string Id, string Name, string? NameAr, string Code, string ZoneId, bool IsActive);

internal sealed class GetDirectoryCitiesHandler(IAppDbContext db) : IRequestHandler<GetDirectoryCitiesQuery, IReadOnlyList<DirectoryCityDto>>
{
    public async Task<IReadOnlyList<DirectoryCityDto>> Handle(GetDirectoryCitiesQuery request, CancellationToken cancellationToken) =>
        await db.Cities.AsNoTracking()
            .Where(c => c.DeletedAt == null && (request.IncludeInactive || c.IsActive))
            .OrderBy(c => c.Name)
            .Select(c => new DirectoryCityDto(c.Id, c.Name, c.NameAr, c.Code, c.ZoneId, c.IsActive))
            .ToListAsync(cancellationToken);
}
