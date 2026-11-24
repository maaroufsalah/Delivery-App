using AutoMapper;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.DeliveryTypes.Queries;

/// <summary>`GET /delivery-types/export` : classeur « Types de livraison ».</summary>
public sealed record ExportDeliveryTypesQuery(GetDeliveryTypesQuery Filter) : IRequest<FileContent>;

internal sealed class ExportDeliveryTypesHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper, IExcelService excel, IClock clock) : IRequestHandler<ExportDeliveryTypesQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<DeliveryTypeListItemDto>> Columns =
    [
        new("Type", r => r.Name),
        new("Multiplicateur", r => r.MultiplierPerMille / 1000m),
        new("Actif", r => Exports.Bool(r.IsActive)),
    ];

    public async Task<FileContent> Handle(ExportDeliveryTypesQuery request, CancellationToken cancellationToken)
    {
        caller.AssertCanSeeInternalPricing("Types de livraison");
        var where = GetDeliveryTypesHandler.Filter(db.DeliveryTypes.AsNoTracking(), request.Filter.Query);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        var rows = await mapper.ProjectTo<DeliveryTypeRow>(Sorting.OrderByKey(where, request.Filter.Query, DeliveryTypeRows.Sorts, "name")).ToListAsync(cancellationToken);
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Types de livraison", Columns, rows.Select(r => r.ToDto()).ToList(), cancellationToken);
    }
}
