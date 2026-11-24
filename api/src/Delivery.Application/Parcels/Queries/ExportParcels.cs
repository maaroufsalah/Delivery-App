using AutoMapper;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Parcels.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Parcels.Queries;

/// <summary>`GET /parcels/export` : classeur « Colis », colonnes financières selon qui exporte.</summary>
public sealed record ExportParcelsQuery(ParcelListFilter Filter) : IRequest<FileContent>;

internal sealed class ExportParcelsHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper, IExcelService excel, IClock clock) : IRequestHandler<ExportParcelsQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<ParcelListItemDto>> Columns =
    [
        new("Code", r => r.Code, Text: true),
        new("Référence client", r => r.ExternalCode ?? string.Empty, Text: true),
        new("Statut", r => DocumentLabels.ParcelStatus(r.StatusId)),
        new("Mis à jour", r => Exports.DateTimeText(r.StatusAt)),
        new("E-commerçant", r => r.ClientName),
        new("Destinataire", r => r.RecipientName),
        new("Téléphone", r => r.RecipientPhone, Text: true),
        new("Ville de départ", r => r.OriginCityName),
        new("Ville de livraison", r => r.DestinationCityName),
        new("Quartier", r => r.DestinationDistrictName ?? string.Empty),
        new("Adresse de livraison", r => r.DeliveryAddress),
        new("Livreur", r => r.CourierName ?? string.Empty),
        new("Montant COD", r => Exports.Amount(r.CodAmount)),
        new("Frais de livraison", r => Exports.Amount(r.DeliveryFee)),
        new("Frais d'échec", r => Exports.Amount(r.FailureFee)),
        new("Frais inclus", r => Exports.Bool(r.FeeIncluded)),
        new("Déjà payé", r => Exports.Bool(r.AlreadyPaid)),
        new("Ramassage offert", r => Exports.Bool(r.PickupWaived)),
        new("Facturation", r => DocumentLabels.Enum(r.BillingState.ToLowerInvariant())),
        new("Créé le", r => Exports.DateTimeText(r.CreatedAt)),
    ];

    private static readonly Dictionary<string, string> MerchantLabels = new()
    {
        ["non-encaisse"] = "Non encaissé",
        ["encaisse"] = "Encaissé",
        ["facture"] = "Facturé",
        ["paye"] = "Payé",
    };

    public async Task<FileContent> Handle(ExportParcelsQuery request, CancellationToken cancellationToken)
    {
        var filter = request.Filter with { Recent = null };
        var (where, _) = await ParcelFilters.InPeriodAsync(db, caller, filter, clock, cancellationToken);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        var rows = await mapper.ProjectTo<ParcelRow>(where.OrderByDescending(p => p.StatusAt).ThenBy(p => p.Code)).ToListAsync(cancellationToken);
        var columns = Columns.ToList();
        if (caller.CanSeeInternalCash())
        {
            columns.Add(new("Statut financier", r => DocumentLabels.Enum(r.Extra.GetValueOrDefault("financialStatus") as string ?? string.Empty)));
            columns.Add(new("Cash rendu par le livreur", r => Exports.Bool(r.Extra.GetValueOrDefault("reimbursedByCourier") as bool?)));
            columns.Add(new("Cash rendu le", r => Exports.DateTimeText(r.Extra.GetValueOrDefault("reimbursedAt") as DateTime?)));
        }
        else
        {
            columns.Add(new("Statut financier", r => r.Extra.GetValueOrDefault("merchantFinancialStatus") is string s ? MerchantLabels.GetValueOrDefault(s, string.Empty) : string.Empty));
        }
        return await Exports.ToXlsxAsync(db, excel, clock, "Colis", columns, rows.Select(r => ParcelViews.ToListItem(r, caller)).ToList(), cancellationToken);
    }
}
