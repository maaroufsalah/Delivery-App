using System.Globalization;
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
using Delivery.Application.Parcels;
using Delivery.Application.PettyCash;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Finance;

/// <summary>`GET /cod-payouts/:code/export` : une ligne par colis, montant gelé.</summary>
public sealed record ExportCodPayoutParcelsQuery(string Code) : IRequest<FileContent>;

internal sealed class ExportCodPayoutParcelsHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportCodPayoutParcelsQuery, FileContent>
{
    private sealed record Line(CodPayoutItemDto Item, string? ExternalCode, string RecipientPhone, int StatusId);

    private static readonly IReadOnlyList<ExportColumn<Line>> Columns =
    [
        new("Code", r => r.Item.ParcelCode, Text: true),
        new("Référence commande", r => r.ExternalCode ?? string.Empty, Text: true),
        new("Destinataire", r => r.Item.RecipientName),
        new("Téléphone", r => r.RecipientPhone, Text: true),
        new("Ville", r => r.Item.DestinationCityName),
        new("Statut", r => DocumentLabels.ParcelStatus(r.StatusId)),
        new("Montant versé", r => Exports.Amount(r.Item.Amount)),
    ];

    public async Task<FileContent> Handle(ExportCodPayoutParcelsQuery request, CancellationToken cancellationToken)
    {
        var payout = await CodPayouts.DetailAsync(db, caller, request.Code, cancellationToken);
        var ids = payout.Items.Select(i => i.ParcelId).ToList();
        var extras = await db.Parcels.AsNoTracking().Where(p => ids.Contains(p.Id)).Select(p => new { p.Id, p.ExternalCode, p.RecipientPhone, p.StatusId }).ToDictionaryAsync(p => p.Id, cancellationToken);
        var rows = payout.Items.Select(i => extras.GetValueOrDefault(i.ParcelId) is { } e ? new Line(i, e.ExternalCode, e.RecipientPhone, e.StatusId) : new Line(i, null, string.Empty, 0)).ToList();
        return await Exports.ToXlsxAsync(db, excel, clock, caller, $"Bon de versement {payout.Code}", Columns, rows, cancellationToken);
    }
}
