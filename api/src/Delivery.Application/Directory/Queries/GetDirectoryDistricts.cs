using System.Text.Json.Serialization;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Directory.Queries;

/// <summary>`GET /directory/districts`.</summary>
public sealed record GetDirectoryDistrictsQuery(string? CityId) : IRequest<IReadOnlyList<DirectoryDistrictDto>>;

public sealed record DirectoryDistrictDto(string Id, string CityId, string Name);

internal sealed class GetDirectoryDistrictsHandler(IAppDbContext db) : IRequestHandler<GetDirectoryDistrictsQuery, IReadOnlyList<DirectoryDistrictDto>>
{
    public async Task<IReadOnlyList<DirectoryDistrictDto>> Handle(GetDirectoryDistrictsQuery request, CancellationToken cancellationToken) =>
        await db.Districts.AsNoTracking()
            .Where(d => d.DeletedAt == null && (string.IsNullOrEmpty(request.CityId) || d.CityId == request.CityId))
            .OrderBy(d => d.CityId).ThenBy(d => d.Name)
            .Select(d => new DirectoryDistrictDto(d.Id, d.CityId, d.Name))
            .ToListAsync(cancellationToken);
}
