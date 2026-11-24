using AutoMapper;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Agencies.Queries;

/// <summary>`GET /agencies/export` : classeur « Agences ».</summary>
public sealed record ExportAgenciesQuery(GetAgenciesQuery Filter) : IRequest<FileContent>;

internal sealed class ExportAgenciesHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper, IExcelService excel, IClock clock) : IRequestHandler<ExportAgenciesQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<AgencyListItemDto>> Columns =
    [
        new("Agence", r => r.Name),
        new("Ville", r => r.CityName),
        new("Téléphone", r => r.Phone ?? string.Empty),
        new("E-mail", r => r.Email ?? string.Empty),
        new("Adresse", r => r.Address ?? string.Empty),
        new("Siège", r => Exports.Bool(r.IsHeadquarters)),
        new("Comptes", r => r.UserCount),
        new("Colis", r => r.ParcelCount),
        new("Active", r => Exports.Bool(r.IsActive)),
    ];

    public async Task<FileContent> Handle(ExportAgenciesQuery request, CancellationToken cancellationToken)
    {
        var where = GetAgenciesHandler.Filter(db.Agencies.AsNoTracking(), request.Filter.Query, caller);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        var rows = await mapper.ProjectTo<AgencyRow>(Sorting.OrderByKey(where, request.Filter.Query, AgencyRows.Sorts, "name")).ToListAsync(cancellationToken);
        var headquarters = await AgencyRows.HeadquartersIdAsync(db, cancellationToken);
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Agences", Columns, rows.Select(r => r.ToDto(headquarters)).ToList(), cancellationToken);
    }
}
