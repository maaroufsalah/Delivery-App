using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Claims;

/// <summary>`GET /claims/export` : classeur « Réclamations », le filtre de l'écran.</summary>
public sealed record ExportClaimsQuery(ClaimListFilter Filter) : IRequest<FileContent>;

internal sealed class ExportClaimsHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportClaimsQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<ClaimListItemDto>> Columns =
    [
        new("Numéro", r => r.Code),
        new("Statut", r => DocumentLabels.Enum(r.Status)),
        new("Type", r => r.TypeLabel),
        new("Objet", r => r.Subject),
        new("E-commerçant", r => r.ClientName),
        new("Colis", r => r.ParcelCode ?? string.Empty),
        new("Assignée à", r => r.AssigneeName ?? string.Empty),
        new("Ouverte par", r => r.AuthorName),
        new("Ouverte le", r => Exports.DateTimeText(r.CreatedAt)),
        new("Échéance", r => Exports.DateTimeText(r.DueAt)),
        new("En retard", r => r.IsLate ? "oui" : "non"),
        new("Traitée le", r => Exports.DateTimeText(r.ResolvedAt)),
    ];

    public async Task<FileContent> Handle(ExportClaimsQuery input, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var (rows, _, _) = ClaimRows.Filter(db, caller, input.Filter, now);
        Exports.AssertWithinLimit(await rows.CountAsync(cancellationToken));
        var items = await rows.OrderByDescending(c => c.CreatedAt).Select(ClaimRows.Row).ToListAsync(cancellationToken);
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Réclamations", Columns, items.Select(r => ClaimRows.Item(r, now)).ToList(), cancellationToken);
    }
}
