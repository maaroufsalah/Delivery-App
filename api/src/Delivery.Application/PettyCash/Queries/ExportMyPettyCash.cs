using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Idempotency;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.PettyCash;

/// <summary>`GET /courier/me/petty-cash/export` : classeur « Mes dépenses ».</summary>
public sealed record ExportMyPettyCashQuery : IRequest<FileContent>;

internal sealed class ExportMyPettyCashHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IExcelService excel) : IRequestHandler<ExportMyPettyCashQuery, FileContent>
{
    private static readonly Dictionary<string, string> Labels = new() { ["draft"] = "Brouillon", ["validated"] = "Validée", ["refused"] = "Refusée" };

    private static readonly IReadOnlyList<ExportColumn<PettyCashMovementDto>> Columns =
    [
        new("Date", r => Exports.CalendarDateText(PettyCashDays.Parse(r.Date))),
        new("Libellé", r => r.Label ?? string.Empty),
        new("Catégorie", r => r.Category ?? string.Empty),
        new("Montant", r => Exports.Amount(r.Amount)),
        new("Statut", r => Labels[r.Status]),
        new("Motif du refus", r => r.RefusalReason ?? string.Empty),
        new("Justificatif", r => r.ProofName ?? string.Empty),
        new("Saisi le", r => Exports.DateTimeText(r.CreatedAt)),
        new("Traité le", r => Exports.DateTimeText(r.DecidedAt)),
    ];

    public async Task<FileContent> Handle(ExportMyPettyCashQuery request, CancellationToken cancellationToken)
    {
        var where = CourierExpenses.Mine(db, caller);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        var rows = await PettyCashMovements.ProjectAsync(db, where.OrderByDescending(m => m.OccurredAt).ThenByDescending(m => m.CreatedAt), cancellationToken);
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Mes dépenses", Columns, rows, cancellationToken);
    }
}
