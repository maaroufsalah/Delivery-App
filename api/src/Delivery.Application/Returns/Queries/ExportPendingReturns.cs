using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.CourierApp;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using Delivery.Domain.Pricing;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Returns;

/// <summary>`GET /returns/pending/export` : « Mes retours » pour un vendeur, « Retours en attente » sinon.</summary>
public sealed record ExportPendingReturnsQuery(string? AgencyId, string? CourierId, string? Search) : IRequest<FileContent>;

internal sealed class ExportPendingReturnsHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportPendingReturnsQuery, FileContent>
{
    private sealed record Row(ReturnParcelLineDto Line, string AgencyName);

    public async Task<FileContent> Handle(ExportPendingReturnsQuery request, CancellationToken cancellationToken)
    {
        var groups = await PendingReturns.ListAsync(db, caller, new GetPendingReturnsQuery(request.AgencyId, request.CourierId, request.Search), cancellationToken);
        var rows = groups.SelectMany(g => g.Parcels.Select(p => new Row(p, g.AgencyName ?? string.Empty))).ToList();
        var merchant = caller.ClientId is not null && !caller.IsSuperAdmin();
        List<ExportColumn<Row>> columns = merchant
            ?
            [
                new("Code", r => r.Line.Code, Text: true),
                new("Référence commande", r => r.Line.ExternalCode ?? string.Empty, Text: true),
                new("Destinataire", r => r.Line.RecipientName),
                new("Téléphone", r => r.Line.RecipientPhone, Text: true),
                new("Ville", r => r.Line.DestinationCityName),
                new("Étape", r => StatusCatalog.MerchantReturnStage(r.Line.StatusId) == "returned" ? "Rendu" : "En route vers vous"),
                new("Date", r => Exports.DateTimeText(r.Line.StatusAt)),
            ]
            :
            [
                new("Code", r => r.Line.Code, Text: true),
                new("Référence commande", r => r.Line.ExternalCode ?? string.Empty, Text: true),
                new("Vendeur", r => r.Line.ClientName),
                new("Destinataire", r => r.Line.RecipientName),
                new("Téléphone", r => r.Line.RecipientPhone, Text: true),
                new("Ville", r => r.Line.DestinationCityName),
                new("Statut", r => DocumentLabels.ParcelStatus(r.Line.StatusId)),
                new("Livreur", r => r.Line.CourierName ?? "Sans livreur"),
                new("Agence", r => r.AgencyName),
                new("Montant", r => Exports.Amount(r.Line.CodAmount)),
                new("Frais de livraison", r => Exports.Amount(r.Line.DeliveryFee)),
                new("Cartons", r => r.Line.Quantity),
                new("En attente depuis", r => Exports.DateTimeText(r.Line.StatusAt)),
            ];
        if (!merchant && caller.CourierId is not null) columns.RemoveAll(c => c.Header == "Frais de livraison");
        return await Exports.ToXlsxAsync(db, excel, clock, caller, merchant ? "Mes retours" : "Retours en attente", columns, rows, cancellationToken);
    }
}
