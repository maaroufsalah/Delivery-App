using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Finance;

/// <summary>`GET /remittances/:code/export` : les colis du bon.</summary>
public sealed record ExportRemittanceParcelsQuery(string Code) : IRequest<FileContent>;

internal sealed class ExportRemittanceParcelsHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportRemittanceParcelsQuery, FileContent>
{
    private sealed record Line(RemittanceItemDto Item, string? ExternalCode, string RecipientPhone, string DestinationCityName, int StatusId);

    private static readonly IReadOnlyList<ExportColumn<Line>> Columns =
    [
        new("Code", r => r.Item.ParcelCode, Text: true),
        new("Référence commande", r => r.ExternalCode ?? string.Empty, Text: true),
        new("Vendeur", r => r.Item.ClientName),
        new("Destinataire", r => r.Item.RecipientName),
        new("Téléphone", r => r.RecipientPhone, Text: true),
        new("Ville", r => r.DestinationCityName),
        new("Statut", r => DocumentLabels.ParcelStatus(r.StatusId)),
        new("Encaissé", r => Exports.Amount(r.Item.Amount)),
        new("Espèces", r => Exports.Amount(r.Item.Cash)),
        new("Chèque", r => Exports.Amount(r.Item.Cheque)),
        new("N° de chèque", r => string.Join(", ", r.Item.Cheques.Select(c => c.Number)), Text: true),
        new("Commission", r => Exports.Amount(r.Item.Commission)),
    ];

    public async Task<FileContent> Handle(ExportRemittanceParcelsQuery request, CancellationToken cancellationToken)
    {
        var remittance = await Remittances.DetailAsync(db, caller, request.Code, cancellationToken);
        var ids = remittance.Items.Select(i => i.ParcelId).ToList();
        var extras = await db.Parcels.AsNoTracking().Where(p => ids.Contains(p.Id)).Select(p => new { p.Id, p.ExternalCode, p.RecipientPhone, City = p.DestinationCity.Name, p.StatusId })
            .ToDictionaryAsync(p => p.Id, cancellationToken);
        var rows = remittance.Items.Select(i => extras.GetValueOrDefault(i.ParcelId) is { } e ? new Line(i, e.ExternalCode, e.RecipientPhone, e.City, e.StatusId) : new Line(i, null, string.Empty, string.Empty, 0))
            .ToList();
        return await Exports.ToXlsxAsync(db, excel, clock, caller, $"Caisse {remittance.Code}", Columns, rows, cancellationToken);
    }
}
