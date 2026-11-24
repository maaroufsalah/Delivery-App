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

/// <summary>`GET /routes/:code/export` : une ligne par colis (le vendeur ne voit que les siens, le livreur sans frais).</summary>
public sealed record ExportRouteParcelsQuery(string Code) : IRequest<FileContent>;

internal sealed class ExportRouteParcelsHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportRouteParcelsQuery, FileContent>
{
    private sealed record Line(string Code, string? ExternalCode, string RecipientName, string RecipientPhone, string City, string? District, string Address, long Cod, long Fee, int StatusId, int Quantity, string Client);

    public async Task<FileContent> Handle(ExportRouteParcelsQuery request, CancellationToken cancellationToken)
    {
        var route = await RouteRows.FindAsync(db, caller, request.Code, cancellationToken);
        var links = db.RouteParcels.AsNoTracking().Where(l => l.RouteId == route.Id);
        if (caller.ClientId is { } clientId) links = links.Where(l => l.Parcel.ClientId == clientId);
        var rows = (await links.OrderBy(l => l.Sequence).ThenBy(l => l.ParcelId)
            .Select(l => new
            {
                l.Parcel.Code, l.Parcel.ExternalCode, l.Parcel.RecipientName, l.Parcel.RecipientPhone, City = l.Parcel.DestinationCity.Name, l.Parcel.DestinationDistrictName,
                DistrictRef = l.Parcel.DestinationDistrict != null ? l.Parcel.DestinationDistrict.Name : null, l.Parcel.DeliveryAddress, l.Parcel.CodAmount, l.Parcel.DeliveryFee, l.Parcel.StatusId,
                l.Parcel.Quantity, Client = l.Parcel.Client.Name,
            }).ToListAsync(cancellationToken))
            .Select(p => new Line(p.Code, p.ExternalCode, p.RecipientName, p.RecipientPhone, p.City, ParcelViews.DistrictLabel(p.DestinationDistrictName, p.DistrictRef), p.DeliveryAddress,
                Money.ToCents(p.CodAmount), Money.ToCents(p.DeliveryFee), p.StatusId, p.Quantity, p.Client)).ToList();
        var columns = new List<ExportColumn<Line>>
        {
            new("Code", r => r.Code, Text: true),
            new("Référence commande", r => r.ExternalCode ?? string.Empty, Text: true),
            new("Destinataire", r => r.RecipientName),
            new("Téléphone", r => r.RecipientPhone, Text: true),
            new("Ville", r => r.City),
            new("Quartier", r => r.District ?? string.Empty),
            new("Adresse", r => r.Address),
            new("Contre-remboursement", r => Exports.Amount(r.Cod)),
            new("Frais de livraison", r => Exports.Amount(r.Fee)),
            new("Statut", r => DocumentLabels.ParcelStatus(r.StatusId)),
            new("Cartons", r => r.Quantity),
            new("Vendeur", r => r.Client),
        };
        if (caller.CourierId is not null) columns.RemoveAll(c => c.Header == "Frais de livraison");
        return await Exports.ToXlsxAsync(db, excel, clock, caller, $"Circuit {route.Code}", columns, rows, cancellationToken);
    }
}
