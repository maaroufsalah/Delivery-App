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

namespace Delivery.Application.Finance;

/// <summary>`GET /courier-statements/export` : classeur « Relevés livreur ».</summary>
public sealed record ExportCourierStatementsQuery(StatementListFilter Filter) : IRequest<FileContent>;

internal sealed class ExportCourierStatementsHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportCourierStatementsQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<CourierStatementListItemDto>> Columns =
    [
        new("Relevé", r => r.Code),
        new("Statut", r => DocumentLabels.Enum(r.Status)),
        new("Livreur", r => r.CourierName),
        new("Agence", r => r.AgencyName),
        new("Début", r => Exports.CalendarDateText(DateOnly.FromDateTime(r.PeriodStart))),
        new("Fin", r => Exports.CalendarDateText(DateOnly.FromDateTime(r.PeriodEnd))),
        new("Mode", r => DocumentLabels.Enum(r.PayModel)),
        new("Règlement des commissions", r => DocumentLabels.Enum(r.SettlementMode)),
        new("Courses", r => r.LineCount),
        new("Fixe", r => Exports.Amount(r.BaseAmount)),
        new("COD encaissé", r => Exports.Amount(r.CodCollected)),
        new("Commissions", r => Exports.Amount(r.CommissionsTotal)),
        new("Déjà gardées aux remises", r => Exports.Amount(r.CommissionsDeducted)),
        new("Reste dû sur les courses", r => Exports.Amount(r.VariableAmount)),
        new("Net à payer", r => Exports.Amount(r.TotalDue)),
        new("Validé le", r => Exports.DateTimeText(r.ValidatedAt)),
        new("Payé le", r => Exports.DateTimeText(r.PaidAt)),
    ];

    public async Task<FileContent> Handle(ExportCourierStatementsQuery request, CancellationToken cancellationToken)
    {
        var where = StatementLists.Filter(db, caller, request.Filter);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        var rows = (await CourierStatements.Project(where).ToListAsync(cancellationToken)).Select(CourierStatements.Wire).ToList();
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Relevés livreur", Columns, rows, cancellationToken);
    }
}
