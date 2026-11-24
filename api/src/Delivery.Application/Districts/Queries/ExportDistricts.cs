using AutoMapper;
using AutoMapper.QueryableExtensions;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Districts.Queries;

/// <summary>`GET /districts/export` : classeur « Quartiers ».</summary>
public sealed record ExportDistrictsQuery(GetDistrictsQuery Filter) : IRequest<FileContent>;

internal sealed class ExportDistrictsHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper, IExcelService excel, IClock clock) : IRequestHandler<ExportDistrictsQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<DistrictListItemDto>> Columns =
    [
        new("Quartier", r => r.Name),
        new("Ville", r => r.CityName),
        new("Colis", r => r.ParcelCount),
        new("Actif", r => Exports.Bool(r.IsActive)),
    ];

    public async Task<FileContent> Handle(ExportDistrictsQuery request, CancellationToken cancellationToken)
    {
        caller.AssertCanSeeInternalPricing("Quartiers");
        var where = GetDistrictsHandler.Filter(db.Districts.AsNoTracking(), request.Filter);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        var rows = await Sorting.OrderByKey(where, request.Filter.Query, DistrictRows.Sorts, "name")
            .ProjectTo<DistrictListItemDto>(mapper.ConfigurationProvider).ToListAsync(cancellationToken);
        return await Exports.ToXlsxAsync(db, excel, clock, "Quartiers", Columns, rows, cancellationToken);
    }
}
