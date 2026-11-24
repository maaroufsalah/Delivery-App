using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.Parcels;
using Delivery.Application.Warehouse.Stock;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Warehouse.Orders;

/// <summary>`GET /orders/export` : classeur « Commandes ».</summary>
public sealed record ExportOrdersQuery(OrderListFilter Filter) : IRequest<FileContent>;

internal sealed class ExportOrdersHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportOrdersQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<OrderListItemDto>> Columns =
    [
        new("Commande", r => r.Code),
        new("Référence externe", r => r.ExternalRef ?? string.Empty),
        new("Statut", r => r.Status),
        new("Origine", r => r.Source),
        new("E-commerçant", r => r.ClientName),
        new("Destinataire", r => r.RecipientName),
        new("Téléphone", r => r.RecipientPhone),
        new("Ville", r => r.DestinationCityName),
        new("Lignes", r => r.LineCount),
        new("Montant", r => Exports.Amount(r.TotalAmount)),
        new("Colis", r => r.ParcelCode ?? string.Empty),
        new("Créée le", r => Exports.DateTimeText(r.CreatedAt)),
    ];

    public async Task<FileContent> Handle(ExportOrdersQuery request, CancellationToken cancellationToken)
    {
        var where = OrderRows.Filter(db.Orders.AsNoTracking(), caller, request.Filter);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        var rows = (await OrderRows.Project(where).ToListAsync(cancellationToken)).Select(r => OrderRows.Fill(new OrderListItemDto(), r)).ToList();
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Commandes", Columns, rows, cancellationToken);
    }
}
