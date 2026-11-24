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
using Delivery.Application.Finance;
using Delivery.Application.Notifications;
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
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Invoices;

/// <summary>`GET /payments/export` : classeur « Règlements ».</summary>
public sealed record ExportPaymentsQuery(PaymentListFilter Filter) : IRequest<FileContent>;

internal sealed class ExportPaymentsHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportPaymentsQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<PaymentListItemDto>> Columns =
    [
        new("Facture", r => r.InvoiceCode ?? string.Empty),
        new("E-commerçant", r => r.ClientName),
        new("Agence", r => r.AgencyName),
        new("Montant", r => Exports.Amount(r.Amount)),
        new("Mode", r => DocumentLabels.Enum(r.Method)),
        new("Compte", r => r.BankAccountLabel ?? string.Empty),
        new("Référence", r => r.Reference ?? string.Empty, Text: true),
        new("Réglé le", r => Exports.DateTimeText(r.PaidAt)),
    ];

    public async Task<FileContent> Handle(ExportPaymentsQuery request, CancellationToken cancellationToken)
    {
        var where = PaymentLists.Filter(db, caller, request.Filter);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Règlements", Columns, await InvoiceRows.PaymentsAsync(db, where, cancellationToken), cancellationToken);
    }
}
