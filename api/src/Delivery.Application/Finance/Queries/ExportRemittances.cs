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

/// <summary>`GET /remittances/export` : classeur « Retours de fonds ».</summary>
public sealed record ExportRemittancesQuery(RemittanceListFilter Filter) : IRequest<FileContent>;

internal sealed class ExportRemittancesHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportRemittancesQuery, FileContent>
{
    private static readonly Dictionary<string, string> StageLabels = new()
    {
        ["open"] = "Caisse ouverte", ["closed"] = "Caisse fermée", ["received"] = "Caisse reçue", ["validated"] = "Caisse validée", ["cancelled"] = "Annulée",
    };

    private static readonly IReadOnlyList<ExportColumn<RemittanceListItemDto>> Columns =
    [
        new("Retour de fonds", r => r.Code),
        new("Statut", r => StageLabels[r.Stage]),
        new("Clôturée par le livreur le", r => Exports.DateTimeText(r.CourierClosedAt)),
        new("Livreur", r => r.CourierName),
        new("Ville", r => r.CityName),
        new("Agence", r => r.AgencyName),
        new("Colis", r => r.ParcelCount),
        new("COD encaissé", r => Exports.Amount(r.ExpectedCod)),
        new("Commissions déduites", r => Exports.Amount(r.ExpectedCommission)),
        new("Net attendu", r => Exports.Amount(r.ExpectedNet)),
        new("Montant remis", r => r.DeclaredAmount is null ? string.Empty : Exports.Amount(r.DeclaredAmount)),
        new("Écart", r => Exports.Amount(r.Variance)),
        new("Motif de l’écart", r => r.VarianceReason ?? string.Empty),
        new("Montant", r => Exports.Amount(r.Amount)),
        new("Confirmé le", r => Exports.DateTimeText(r.ConfirmedAt)),
        new("Confirmé par", r => r.ConfirmedByName ?? string.Empty),
        new("Créé le", r => Exports.DateTimeText(r.CreatedAt)),
    ];

    public async Task<FileContent> Handle(ExportRemittancesQuery request, CancellationToken cancellationToken)
    {
        var where = RemittanceLists.Filter(db, caller, request.Filter);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Retours de fonds", Columns, await Remittances.ListAsync(where, cancellationToken), cancellationToken);
    }
}
