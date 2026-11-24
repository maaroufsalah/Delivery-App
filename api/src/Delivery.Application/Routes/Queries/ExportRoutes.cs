using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Routes;

/// <summary>`GET /routes/export` : classeur « Circuits » (toujours par changement de statut).</summary>
public sealed record ExportRoutesQuery(RouteListFilter Filter) : IRequest<FileContent>;

internal sealed class ExportRoutesHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportRoutesQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<RouteListItemDto>> Columns =
    [
        new("Circuit", r => r.Code),
        new("Statut", r => DocumentLabels.Enum(r.Status)),
        new("Type", r => DocumentLabels.Enum(r.Kind)),
        new("Livreur", r => r.CourierName),
        new("Ville", r => r.CityName),
        new("Colis", r => r.ParcelCount),
        new("Confirmés", r => r.ConfirmedCount),
        new("Sortie", r => Exports.DateText(r.ExitDate)),
        new("Mis à jour", r => Exports.DateTimeText(r.StatusAt)),
    ];

    public async Task<FileContent> Handle(ExportRoutesQuery request, CancellationToken cancellationToken)
    {
        var where = RouteRows.Filter(db.Routes.AsNoTracking(), caller, request.Filter);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        var rows = await RouteRows.Project(where.OrderByDescending(r => r.StatusAt)).ToListAsync(cancellationToken);
        var removed = await RouteRows.RemovedCountsAsync(db, rows.Select(r => r.Id).ToList(), cancellationToken);
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Circuits", Columns, rows.Select(r => RouteRows.Fill(new RouteListItemDto(), r, removed.GetValueOrDefault(r.Id))).ToList(), cancellationToken);
    }
}
