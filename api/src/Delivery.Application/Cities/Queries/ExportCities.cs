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

/// <summary>`GET /cities/export` : le même filtre, en classeur « Villes ».</summary>
public sealed record ExportCitiesQuery(GetCitiesQuery Filter) : IRequest<FileContent>;

internal sealed class ExportCitiesHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper, IExcelService excel, IClock clock) : IRequestHandler<ExportCitiesQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<CityListItemDto>> Columns =
    [
        new("Ville", r => r.Name),
        new("Code", r => r.Code),
        new("Zone", r => r.ZoneName),
        new("Quartiers", r => r.DistrictCount),
        new("Colis", r => r.ParcelCount),
        new("Active", r => Exports.Bool(r.IsActive)),
    ];

    public async Task<FileContent> Handle(ExportCitiesQuery request, CancellationToken cancellationToken)
    {
        caller.AssertCanSeeInternalPricing("Villes");
        var where = GetCitiesHandler.Filter(db.Cities.AsNoTracking(), request.Filter);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        var rows = await Sorting.OrderByKey(where, request.Filter.Query, CityRows.Sorts, "name")
            .ProjectTo<CityListItemDto>(mapper.ConfigurationProvider).ToListAsync(cancellationToken);
        return await Exports.ToXlsxAsync(db, excel, clock, "Villes", Columns, await CityRows.WithCountsAsync(db, rows, cancellationToken), cancellationToken);
    }
}
