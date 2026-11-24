using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Bl;

/// <summary>`GET /bl/delivery-slips/export` : classeur « Bons de livraison ».</summary>
public sealed record ExportDeliverySlipsQuery(ListQuery Paging, DeliverySlipFilter Filter) : IRequest<FileContent>;

public sealed class ExportDeliverySlipsValidator : ZodValidator<ExportDeliverySlipsQuery>
{
    public ExportDeliverySlipsValidator()
    {
        RuleFor(x => x.Filter.Status).OneOf(BlRows.SlipStatuses).OverridePropertyName("status");
    }
}

internal sealed class ExportDeliverySlipsHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportDeliverySlipsQuery, FileContent>
{
    private static readonly Dictionary<string, string> States = new()
    {
        ["collected"] = "Récupéré par le livreur", ["handed-over"] = "Remis à l’agence (liasse)", ["received"] = "Réceptionné", ["returned"] = "Récupéré par le vendeur", ["closed"] = "Clôturé",
    };

    private static readonly IReadOnlyList<ExportColumn<DeliverySlipListItemDto>> Columns =
    [
        new("Colis", r => r.ParcelCode),
        new("Destinataire", r => r.RecipientName),
        new("Client Pro", r => r.ClientName),
        new("Livreur", r => r.CourierName ?? string.Empty),
        new("Référence BL", r => r.Reference ?? string.Empty),
        new("Livré le", r => Exports.DateTimeText(r.DeliveredAt)),
        new("Récupéré le", r => Exports.DateTimeText(r.CollectedAt)),
        new("Réceptionné le", r => Exports.DateTimeText(r.ReceivedAt)),
        new("Remis au vendeur le", r => Exports.DateTimeText(r.ReturnedAt)),
        new("Remis à", r => r.ReturnedToName ?? string.Empty),
        new("État", r => States[r.Status]),
    ];

    public async Task<FileContent> Handle(ExportDeliverySlipsQuery request, CancellationToken cancellationToken)
    {
        var where = await BlRows.FilterAsync(db, caller, request.Filter, cancellationToken);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        var ordered = request.Paging.Order == "asc" ? where.OrderBy(s => s.CollectedAt).ThenBy(s => s.Id) : where.OrderByDescending(s => s.CollectedAt).ThenBy(s => s.Id);
        var rows = await BlRows.Project(ordered).ToListAsync(cancellationToken);
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Bons de livraison", Columns, rows, cancellationToken);
    }
}
