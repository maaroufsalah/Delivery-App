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

/// <summary>`GET /return-slips/export` : classeur « Bons de retour ».</summary>
public sealed record ExportReturnSlipsQuery(SlipListFilter Filter) : IRequest<FileContent>;

internal sealed class ExportReturnSlipsHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportReturnSlipsQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<ReturnSlipListItemDto>> Columns =
    [
        new("Bon de retour", r => r.Code),
        new("Statut", r => DocumentLabels.Enum(r.Status)),
        new("E-commerçant", r => r.ClientName),
        new("Agence", r => r.AgencyName),
        new("Colis", r => r.ParcelCount),
        new("Total frais", r => Exports.Amount(r.TotalFees)),
        new("Remis le", r => Exports.DateTimeText(r.DeliveredAt)),
        new("Reçu par", r => r.ReceivedByName ?? string.Empty),
        new("Créé le", r => Exports.DateTimeText(r.CreatedAt)),
    ];

    public async Task<FileContent> Handle(ExportReturnSlipsQuery request, CancellationToken cancellationToken)
    {
        var where = SlipLists.Filter(db, caller, request.Filter);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Bons de retour", Columns, await Slips.Project(where).ToListAsync(cancellationToken), cancellationToken);
    }
}
