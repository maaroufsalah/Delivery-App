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

/// <summary>`GET /courier-stock/parcels/export` : classeur « Stock livreur » d'un livreur.</summary>
public sealed record ExportCourierStockParcelsQuery(string? CourierId, ListQueryParams Query, string? StatusId, string[]? ClientId, string[]? CityId, string[]? RouteId) : IRequest<FileContent>
{
    public CourierStockParcelFilter Filter => CourierStocks.ParcelFilter(Query, StatusId, ClientId, CityId, RouteId);
}

internal sealed class ExportCourierStockParcelsHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportCourierStockParcelsQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<CourierStockParcelDto>> Columns =
    [
        new("Code", r => r.Code),
        new("Destinataire", r => r.RecipientName),
        new("Téléphone", r => r.RecipientPhone),
        new("Ville de destination", r => r.DestinationCityName),
        new("Vendeur", r => r.ClientName),
        new("COD (MAD)", r => Exports.Amount(r.CodInCents)),
        new("Statut", r => DocumentLabels.ParcelStatus(r.StatusId)),
        new("Circuit", r => r.RouteCode ?? string.Empty),
        new("Affecté le", r => Exports.DateTimeText(r.AssignedAt)),
        new("Dernier mouvement", r => Exports.DateTimeText(r.LastMovementAt)),
    ];

    public async Task<FileContent> Handle(ExportCourierStockParcelsQuery request, CancellationToken cancellationToken)
    {
        var courierId = request.CourierId?.Trim();
        if (string.IsNullOrEmpty(courierId))
        {
            throw BusinessException.Validation("Saisie invalide.", new { issues = new[] { new { path = "courierId", message = request.CourierId is null ? Common.Validation.Zod.Expected("string") : Common.Validation.Zod.TooSmallString(1) } } });
        }
        var selection = await CourierStocks.SelectionAsync(db, caller, courierId, request.Filter, cancellationToken);
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Stock livreur", Columns, selection.Parcels.Select(p => p.View).ToList(), cancellationToken);
    }
}
