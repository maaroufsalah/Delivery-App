using System.Globalization;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Returns;

/// <summary>`GET /return-handovers/export` : classeur « Remises ».</summary>
public sealed record ExportReturnHandoversQuery(HandoverListFilter Filter) : IRequest<FileContent>;

internal sealed class ExportReturnHandoversHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportReturnHandoversQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<ReturnHandoverListItemDto>> Columns =
    [
        new("Bon de retour", r => r.Code),
        new("Statut", r => DocumentLabels.Enum(r.Status)),
        new("Origine", r => DocumentLabels.Enum(r.Origin)),
        new("Livreur", r => r.CourierName),
        new("Agence", r => r.AgencyName),
        new("Vendeur(s)", r => string.Join(", ", r.ClientNames)),
        new("Colis", r => r.ParcelCount),
        new("Pris en charge le", r => Exports.DateTimeText(r.PickedUpAt)),
        new("Remis le", r => Exports.DateTimeText(r.HandedOverAt)),
        new("Reçu le", r => Exports.DateTimeText(r.ConfirmedAt)),
        new("Reçu par", r => r.ConfirmedByName ?? string.Empty),
        new("Clôturé le", r => Exports.DateTimeText(r.ClosedAt)),
        new("Créé le", r => Exports.DateTimeText(r.CreatedAt)),
        new("Créé par", r => r.CreatedByName ?? string.Empty),
    ];

    public async Task<FileContent> Handle(ExportReturnHandoversQuery request, CancellationToken cancellationToken)
    {
        var where = HandoverLists.Filter(db, caller, request.Filter);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Remises", Columns, await Handovers.ListAsync(where, cancellationToken), cancellationToken);
    }
}
