using System.Globalization;
using System.Text.Json;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Invoices;

/// <summary>`GET /transfer-orders/:code/xlsx` : les lignes et une ligne « Total ».</summary>
public sealed record GetTransferOrderXlsxQuery(string Code) : IRequest<FileContent>;

internal sealed class GetTransferOrderXlsxHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<GetTransferOrderXlsxQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<TransferLineDto>> Columns =
    [
        new("Client", r => r.ClientName),
        new("Montant global (COD encaissé)", r => Exports.Amount(r.Collected)),
        new("Frais de livraison", r => Exports.Amount(r.Fees)),
        new("Montant à verser", r => Exports.Amount(r.Due)),
    ];

    public async Task<FileContent> Handle(GetTransferOrderXlsxQuery request, CancellationToken cancellationToken)
    {
        var order = await TransferOrders.DetailAsync(db, caller, request.Code, cancellationToken);
        var total = new TransferLineDto(string.Empty, "Total", order.Lines.Sum(l => l.ParcelCount), order.TotalCollected, order.TotalFees, order.TotalDue);
        var file = await Exports.ToXlsxAsync(db, excel, clock, caller, order.Code, Columns, [.. order.Lines, total], cancellationToken);
        return file with { FileName = $"{TransferOrders.BaseName(order)}.xlsx" };
    }
}
