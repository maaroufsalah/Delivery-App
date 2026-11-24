using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.Parcels.Labels;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.CourierStock;

/// <summary>`CourierStockPdfService` : le PDF de la situation, direct jusqu'à 1 500 lignes, sinon en arrière-plan.</summary>
public static class CourierStockPdfs
{
    public const int AsyncThreshold = 1_500;

    public sealed record Prepared(CourierStockSheet Data, string FileName, int Rows);

    private static CourierStockSheetParcel ToPdf(CourierStockParcelDto p) =>
        new(p.Code, p.RecipientName, p.RecipientPhone, p.DestinationCityName, p.ClientName, p.StatusId, p.RouteCode, p.CodInCents);

    private static List<CourierStockSheetStatusLine> ByStatus(IEnumerable<CourierStockParcelDto> parcels) =>
        parcels.GroupBy(p => p.StatusId).Select(g => new CourierStockSheetStatusLine(g.Key, g.Count(), g.Sum(p => p.CodInCents))).OrderBy(l => l.StatusId).ToList();

    public static async Task<Prepared> IndividualAsync(IAppDbContext db, ICurrentUser caller, IClock clock, IFileStorage storage, string courierId, CourierStockParcelFilter filter,
        CancellationToken cancellationToken)
    {
        var selection = await CourierStocks.SelectionAsync(db, caller, courierId, filter, cancellationToken);
        var data = await DocumentAsync(db, clock, storage, [new CourierStockSheetSection(selection.Courier.Name, selection.Courier.AgencyName, selection.Counters,
            selection.ByStatus.Select(l => new CourierStockSheetStatusLine(l.StatusId, l.Count, l.CodInCents)).ToList(), selection.Parcels.Select(p => ToPdf(p.View)).ToList())], null, cancellationToken);
        return new Prepared(data, $"Stock-{Slug(selection.Courier.Name)}-{Stamp(data.GeneratedAt)}.pdf", selection.Parcels.Count);
    }

    public static async Task<Prepared> GlobalAsync(IAppDbContext db, ICurrentUser caller, IClock clock, IFileStorage storage, CourierStockListFilter filter, CancellationToken cancellationToken)
    {
        var (rows, totals) = await CourierStocks.SnapshotAsync(db, caller, filter, cancellationToken);
        var agencyIds = filter.AgencyId is not null && Common.Security.Scope.MayFilterBy(caller, caller.AgencyId) ? filter.AgencyId : null;
        var parcels = (await CourierStocks.ParcelsAsync(db, caller, agencyIds, null, cancellationToken)).GroupBy(p => p.CourierId).ToDictionary(g => g.Key, g => g.Select(p => p.View).ToList());
        var sections = rows.Where(r => parcels.ContainsKey(r.CourierId)).Select(r => new CourierStockSheetSection(r.CourierName, r.AgencyName, r, ByStatus(parcels[r.CourierId]),
            parcels[r.CourierId].Select(ToPdf).ToList())).ToList();
        var filters = new List<string>();
        if (filter.AgencyId is { Count: > 0 } ids)
        {
            var names = await db.Agencies.AsNoTracking().Where(a => ids.Contains(a.Id)).OrderBy(a => a.Name).Select(a => a.Name).ToListAsync(cancellationToken);
            if (names.Count > 0) filters.Add($"Agence {string.Join(", ", names)}");
        }
        if (!string.IsNullOrEmpty(filter.Search)) filters.Add($"Recherche « {filter.Search} »");
        if (filter.All == "true") filters.Add("Livreurs sans stock compris");
        var summary = new CourierStockSheetSummary(rows.Select(r => new CourierStockSheetRow(r.CourierName, r.AgencyName, r)).ToList(), totals, filters);
        var data = await DocumentAsync(db, clock, storage, sections, summary, cancellationToken);
        return new Prepared(data, $"Stock-Livreurs-{Stamp(data.GeneratedAt)}.pdf", sections.Sum(s => s.Parcels.Count));
    }

    private static async Task<CourierStockSheet> DocumentAsync(IAppDbContext db, IClock clock, IFileStorage storage, IReadOnlyList<CourierStockSheetSection> sections,
        CourierStockSheetSummary? summary, CancellationToken cancellationToken)
    {
        var company = await PdfCompany.LoadAsync(db, "Stock", cancellationToken);
        var identity = await db.Companies.AsNoTracking().OrderBy(c => c.CreatedAt).Select(c => new { c.Id, c.ShortName, c.LogoUrl }).FirstOrDefaultAsync(cancellationToken);
        byte[]? logo = null;
        // `companyLogoDataUri` : un logo SVG ne passe pas dans le PDF.
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
        return new CourierStockSheet(company, identity?.ShortName, logo, clock.UtcNow, summary, sections);
    }

    public static FileContent RenderSync(IPdfService pdf, Prepared prepared)
    {
        if (prepared.Rows > AsyncThreshold)
        {
            throw BusinessException.Validation($"Plus de {AsyncThreshold} colis : le PDF se prépare en arrière-plan.",
                new { reason = "stock-pdf-async", count = prepared.Rows, threshold = AsyncThreshold });
        }
        return ParcelLabels.Pdf(pdf.Render(prepared.Data), prepared.FileName);
    }

    /// <summary>`slug` : le nom du livreur sans accents ni ponctuation.</summary>
    public static string Slug(string value)
    {
        var builder = new StringBuilder();
        foreach (var ch in value.Normalize(NormalizationForm.FormD)) if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) builder.Append(ch);
        var slug = Regex.Replace(builder.ToString(), "[^A-Za-z0-9]+", "-").Trim('-');
        return slug.Length == 0 ? "livreur" : slug;
    }

    /// <summary>`stamp` : l'instant UTC en AAAAMMJJHHMM.</summary>
    public static string Stamp(DateTime at) => at.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
}
