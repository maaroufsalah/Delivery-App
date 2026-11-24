using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using Delivery.Domain.Parcels;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.CourierStock;

/// <summary>`GET /courier-stock/export` : classeur « Stock livreurs ».</summary>
public sealed record ExportCourierStockQuery(ListQueryParams Query, string[]? AgencyId, string? All) : IRequest<FileContent>
{
    public CourierStockListFilter Filter => CourierStocks.ListFilter(Query, AgencyId, All);
}

internal sealed class ExportCourierStockHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportCourierStockQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<CourierStockRowDto>> Columns =
    [
        new("Livreur", r => r.CourierName),
        new("Téléphone", r => r.CourierPhone),
        new("Agence", r => r.AgencyName ?? string.Empty),
        new("Colis en main", r => r.InHand),
        new("dont ramassés non validés", r => r.PickedUp),
        new("dont en attente / affectés", r => r.Awaiting),
        new("dont en cours de livraison", r => r.OutForDelivery),
        new("dont échec / à retourner", r => r.FailedOrReturning),
        new("Livrés non remis", r => r.DeliveredUnremitted),
        new("COD encaissé non remis (MAD)", r => Exports.Amount(r.CollectedUnremittedInCents)),
        new("COD à encaisser (MAD)", r => Exports.Amount(r.ToCollectInCents)),
        new("Dernier mouvement", r => Exports.DateTimeText(r.LastMovementAt)),
    ];

    public async Task<FileContent> Handle(ExportCourierStockQuery request, CancellationToken cancellationToken)
    {
        var (rows, _) = await CourierStocks.SnapshotAsync(db, caller, request.Filter, cancellationToken);
        return await Exports.ToXlsxAsync(db, excel, clock, "Stock livreurs", Columns, rows, cancellationToken);
    }
}
