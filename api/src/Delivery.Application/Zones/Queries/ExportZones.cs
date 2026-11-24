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

/// <summary>`GET /zones/export` : classeur « Zones ».</summary>
public sealed record ExportZonesQuery(GetZonesQuery Filter) : IRequest<FileContent>;

internal sealed class ExportZonesHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper, IExcelService excel, IClock clock) : IRequestHandler<ExportZonesQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<ZoneListItemDto>> Columns =
    [
        new("Zone", r => r.Name),
        new("Tarif inter-ville", r => Exports.Amount(r.BaseRate)),
        new("Tarif même ville", r => Exports.Amount(r.SameCityRate)),
        new("Délai (jours)", r => r.DeliveryDays),
        new("Villes", r => r.CityCount),
        new("Active", r => Exports.Bool(r.IsActive)),
    ];

    public async Task<FileContent> Handle(ExportZonesQuery request, CancellationToken cancellationToken)
    {
        caller.AssertCanSeeInternalPricing("Zones");
        var where = GetZonesHandler.Filter(db.Zones.AsNoTracking(), request.Filter.Query);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        var rows = await mapper.ProjectTo<ZoneRow>(Sorting.OrderByKey(where, request.Filter.Query, ZoneRows.Sorts, "name")).ToListAsync(cancellationToken);
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Zones", Columns, rows.Select(r => r.ToDto()).ToList(), cancellationToken);
    }
}
