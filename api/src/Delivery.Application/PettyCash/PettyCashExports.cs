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

internal static class PettyCashExports
{
    public static readonly IReadOnlyList<ExportColumn<PettyCashMovementDto>> Columns =
    [
        new("Date", r => Exports.CalendarDateText(PettyCashDays.Parse(r.Date))),
        new("Type", r => r.Kind == "supply" ? "Approvisionnement" : "Dépense"),
        new("Libellé", r => r.Label ?? string.Empty),
        new("Catégorie", r => r.Category ?? string.Empty),
        new("Montant", r => Exports.Amount(r.Kind == "expense" ? -r.Amount : r.Amount)),
        new("Acheté par", r => r.BuyerName ?? string.Empty),
        new("Agence", r => r.AgencyName),
        new("Note", r => r.Note ?? string.Empty),
        new("Justificatif", r => r.ProofName ?? string.Empty),
        new("Saisi par", r => r.CreatedByName ?? string.Empty),
        new("Saisi le", r => Exports.DateTimeText(r.CreatedAt)),
    ];

    /// <summary>`dayQuery` : la journée seule, mouvements validés, dans l'ordre de saisie.</summary>
    public static PettyCashFilter Day(string day, string? agencyId) =>
        new(new ListQuery { Page = 1, PageSize = 25, Order = "asc" }, "custom", day, day, agencyId, null, null, null, ["validated"]);

    public static async Task<FileContent> PdfAsync(IAppDbContext db, ICurrentUser caller, IClock clock, IPdfService pdf, IFileStorage storage, PettyCashFilter filter, PettyCashDaySummaryDto? day,
        CancellationToken cancellationToken)
    {
        var (rows, first) = await PettyCashMovements.ValidatedAsync(db, caller, clock, filter, PettyCashMovements.PdfLimit, cancellationToken);
        var company = await PdfCompany.LoadAsync(db, "Petite caisse", cancellationToken);
        var identity = await db.Companies.AsNoTracking().OrderBy(c => c.CreatedAt).Select(c => new { c.Id, c.LogoUrl }).FirstOrDefaultAsync(cancellationToken);
        byte[]? logo = null;
        if (identity is not null && Settings.Logos.KeyOf(identity.Id, identity.LogoUrl) is { } key && !key.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                logo = await storage.GetAsync(key, cancellationToken);
            }
            catch (BusinessException)
            {
                logo = null;
            }
        }
        var generatedAt = clock.UtcNow;
        var sheet = new PettyCashSheet(company, logo, generatedAt, first.From, first.To, first.Totals.Balance, first.Totals.Supplied, first.Totals.Spent,
            day is null ? null : new PettyCashSheetDay(day.Status, day.OpeningBalance, day.Supplied, day.Spent, day.ClosingBalance, day.ClosedAt, day.ClosedBy, day.PendingDrafts, day.ReopenReason),
            rows.Select(r => new PettyCashSheetLine(r.Date, r.Kind, r.Label, r.Note, r.Category, r.BuyerName, r.Amount)).ToList());
        var fileName = day is null ? $"Petite-Caisse-{generatedAt:yyyy-MM-dd}.pdf" : $"Petite-Caisse-Journee-{day.Day}.pdf";
        return new FileContent(pdf.Render(sheet), FileContent.Pdf, fileName, CacheControl: "private, no-store");
    }
}
