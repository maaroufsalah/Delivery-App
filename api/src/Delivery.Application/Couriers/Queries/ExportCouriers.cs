using AutoMapper;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Couriers.Queries;

/// <summary>`GET /couriers/export` : classeur « Livreurs ».</summary>
public sealed record ExportCouriersQuery(GetCouriersQuery Filter) : IRequest<FileContent>;

internal sealed class ExportCouriersHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper, IExcelService excel, IClock clock) : IRequestHandler<ExportCouriersQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<CourierListItemDto>> Columns =
    [
        new("Nom", r => $"{r.FirstName} {r.LastName}"),
        new("Téléphone", r => r.Phone, Text: true),
        new("Ville", r => r.CityName),
        new("Agence", r => r.AgencyName ?? string.Empty),
        new("Véhicule", r => r.VehicleType),
        new("Rémunération", r => r.PayModel),
        new("Période", r => r.PayPeriod),
        new("Colis", r => r.ParcelCount),
        new("Validé le", r => Exports.DateText(r.ValidatedAt)),
        new("Actif", r => Exports.Bool(r.IsActive)),
    ];

    public async Task<FileContent> Handle(ExportCouriersQuery request, CancellationToken cancellationToken)
    {
        List<CourierListItemDto> rows = [];
        if (caller.ClientId is null || caller.IsSuperAdmin())
        {
            var where = GetCouriersHandler.Filter(db.Couriers.AsNoTracking(), request.Filter, caller);
            Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
            rows = await CourierRows.ProjectAsync<CourierListItemDto>(Sorting.OrderByKey(where, request.Filter.Query, CourierRows.Sorts, "lastName"), mapper, cancellationToken);
        }
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Livreurs", Columns, rows, cancellationToken);
    }
}
