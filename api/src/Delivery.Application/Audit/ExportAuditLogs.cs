using System.Text.Json;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Audit;

/// <summary>`GET /audit/export` : classeur « Journal ».</summary>
public sealed record ExportAuditLogsQuery(AuditListFilter Filter) : IRequest<FileContent>;

internal sealed class ExportAuditLogsHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportAuditLogsQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<AuditLogListItemDto>> Columns =
    [
        new("Date", r => Exports.DateTimeText(r.At)),
        new("Entité", r => r.Entity),
        new("Identifiant", r => r.EntityId),
        new("Action", r => DocumentLabels.Enum(r.Action)),
        new("Champs modifiés", r => string.Join(", ", r.Diff)),
        new("Utilisateur", r => r.UserEmail ?? string.Empty),
        new("Rôle", r => r.Role ?? string.Empty),
        new("Adresse IP", r => r.Ip ?? string.Empty),
    ];

    public async Task<FileContent> Handle(ExportAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var where = await AuditRows.FilterAsync(db, caller, request.Filter, cancellationToken);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        var rows = (await where.ToListAsync(cancellationToken)).Select(r => AuditRows.Fill(new AuditLogListItemDto(), r)).ToList();
        return await Exports.ToXlsxAsync(db, excel, clock, "Journal", Columns, rows, cancellationToken);
    }
}
