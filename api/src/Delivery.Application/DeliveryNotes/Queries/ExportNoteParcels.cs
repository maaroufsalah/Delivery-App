using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.DeliveryNotes;

/// <summary>`GET /delivery-notes/:code/export` : les colis du bon (sans les frais pour un livreur).</summary>
public sealed record ExportNoteParcelsQuery(string Code) : IRequest<FileContent>;

internal sealed class ExportNoteParcelsHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportNoteParcelsQuery, FileContent>
{
    private sealed record Line(NoteParcelDto Parcel, string? ExternalCode, string RecipientPhone);

    public async Task<FileContent> Handle(ExportNoteParcelsQuery request, CancellationToken cancellationToken)
    {
        var note = await NoteRows.DetailAsync(db, caller, request.Code, cancellationToken);
        var ids = note.Parcels.Select(p => p.Id).ToList();
        var extras = await db.Parcels.AsNoTracking().Where(p => ids.Contains(p.Id)).Select(p => new { p.Id, p.ExternalCode, p.RecipientPhone }).ToDictionaryAsync(p => p.Id, cancellationToken);
        var columns = new List<ExportColumn<Line>>
        {
            new("Code", r => r.Parcel.Code, Text: true),
            new("Référence commande", r => r.ExternalCode ?? string.Empty, Text: true),
            new("Destinataire", r => r.Parcel.RecipientName),
            new("Téléphone", r => r.RecipientPhone, Text: true),
            new("Ville", r => r.Parcel.DestinationCityName),
            new("Quartier", r => r.Parcel.DestinationDistrictName ?? string.Empty),
            new("Désignation", r => r.Parcel.Description),
            new("Statut", r => DocumentLabels.ParcelStatus(r.Parcel.StatusId)),
            new("Contre-remboursement", r => Exports.Amount(r.Parcel.CodAmount)),
            new("Frais de livraison", r => Exports.Amount(r.Parcel.DeliveryFee)),
            new("Cartons", r => r.Parcel.Quantity),
            new("Client corrigé", r => NoteRows.ClientCorrectionText(r.Parcel.ClientCorrection)),
            new("Motif de la correction", r => r.Parcel.ClientCorrection?.Reason ?? string.Empty),
        };
        if (caller.CourierId is not null) columns.RemoveAll(c => c.Header == "Frais de livraison");
        var rows = note.Parcels.Select(p => new Line(p, extras.GetValueOrDefault(p.Id)?.ExternalCode, extras.GetValueOrDefault(p.Id)?.RecipientPhone ?? string.Empty)).ToList();
        return await Exports.ToXlsxAsync(db, excel, clock, caller, $"Bon {note.Code}", columns, rows, cancellationToken);
    }
}
