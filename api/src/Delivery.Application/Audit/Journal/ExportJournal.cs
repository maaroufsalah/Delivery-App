using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Audit.Journal;

/// <summary>`GET /audit/journal/export` : classeur « Journal », le nom court de la société substitué.</summary>
public sealed record ExportJournalQuery(JournalFilter Filter) : IRequest<FileContent>;

internal sealed class ExportJournalHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportJournalQuery, FileContent>
{
    public async Task<FileContent> Handle(ExportJournalQuery request, CancellationToken cancellationToken)
    {
        var company = await db.Companies.AsNoTracking().OrderBy(c => c.CreatedAt).Select(c => new { c.ShortName, c.Name }).FirstOrDefaultAsync(cancellationToken);
        // `companyShortName` : le nom court, sinon les 20 premiers caractères du nom.
        var name = company?.Name.Trim() ?? string.Empty;
        var shortName = !string.IsNullOrWhiteSpace(company?.ShortName) ? company.ShortName.Trim() : name.Length > 20 ? name[..20] : name;
        string Short(string text) => text.Replace("%shortName%", shortName, StringComparison.Ordinal);
        IReadOnlyList<ExportColumn<JournalEntryDetailDto>> columns =
        [
            new("Date", r => Exports.DateTimeText(r.At)),
            new("Action", r => Short(JournalPresenter.ActionText(r))),
            new("Objet", r => Short(JournalPresenter.ObjectText(r))),
            new("Utilisateur", r => Short(r.User?.Name ?? "Système")),
            new("Rôle", r => Short(r.User is null ? string.Empty : JournalCatalog.RoleLabels.GetValueOrDefault(r.User.Role, string.Empty))),
            new("E-mail", r => Short(r.User?.Email ?? string.Empty)),
            new("Source", r => Short(JournalCatalog.SourceLabels.GetValueOrDefault(r.Source, r.Source))),
            new("Agence", r => Short(r.Agency ?? string.Empty)),
            new("Détail", r => Short(string.Join(" ; ", r.Changes.Select(JournalPresenter.ChangeText)))),
            new("Motif", r => Short(r.Reason ?? string.Empty)),
        ];
        var all = new List<JournalEntryDetailDto>();
        for (var page = 1; ; page++)
        {
            var (items, total) = await JournalReader.PageAsync(db, caller, request.Filter with { Page = page, PageSize = 200 }, cancellationToken);
            if (page == 1) Exports.AssertWithinLimit(total);
            all.AddRange(items);
            if (items.Count == 0 || all.Count >= total) break;
        }
        return await Exports.ToXlsxAsync(db, excel, clock, "Journal", columns, all, cancellationToken);
    }
}
