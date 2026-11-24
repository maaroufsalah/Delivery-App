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

/// <summary>`GET /return-slips/:code/export` : les colis du bon (sans les frais pour un livreur).</summary>
public sealed record ExportSlipParcelsQuery(string Code) : IRequest<FileContent>;

internal sealed class ExportSlipParcelsHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportSlipParcelsQuery, FileContent>
{
    private static readonly Dictionary<string, string> Reasons = new()
    {
        ["refused"] = "Refusé", ["unreachable"] = "Injoignable", ["cancelled"] = "Annulé", ["damaged"] = "Endommagé", ["wrong-address"] = "Adresse erronée", ["other"] = "Autre",
    };

    private sealed record Line(ReturnSlipItemDto Item, string? ExternalCode, string RecipientPhone, string DestinationCityName, int StatusId);

    public async Task<FileContent> Handle(ExportSlipParcelsQuery request, CancellationToken cancellationToken)
    {
        var slip = await Slips.DetailAsync(db, caller, request.Code, cancellationToken);
        var ids = slip.Items.Select(i => i.ParcelId).ToList();
        var extras = await db.Parcels.AsNoTracking().Where(p => ids.Contains(p.Id)).Select(p => new { p.Id, p.ExternalCode, p.RecipientPhone, City = p.DestinationCity.Name, p.StatusId })
            .ToDictionaryAsync(p => p.Id, cancellationToken);
        var columns = new List<ExportColumn<Line>>
        {
            new("Code", r => r.Item.ParcelCode, Text: true),
            new("Référence commande", r => r.ExternalCode ?? string.Empty, Text: true),
            new("Destinataire", r => r.Item.RecipientName),
            new("Téléphone", r => r.RecipientPhone, Text: true),
            new("Ville", r => r.DestinationCityName),
            new("Motif", r => Reasons.GetValueOrDefault(r.Item.Reason, r.Item.Reason)),
            new("Statut", r => DocumentLabels.ParcelStatus(r.StatusId)),
            new("Frais de retour", r => Exports.Amount(r.Item.FeeApplied)),
            new("Cartons", r => r.Item.Quantity),
        };
        if (caller.CourierId is not null) columns.RemoveAll(c => c.Header == "Frais de retour");
        var rows = slip.Items.Select(i => extras.GetValueOrDefault(i.ParcelId) is { } e ? new Line(i, e.ExternalCode, e.RecipientPhone, e.City, e.StatusId) : new Line(i, null, string.Empty, i.DestinationCityName, i.StatusId))
            .ToList();
        return await Exports.ToXlsxAsync(db, excel, clock, caller, $"Bon de retour {slip.Code}", columns, rows, cancellationToken);
    }
}
