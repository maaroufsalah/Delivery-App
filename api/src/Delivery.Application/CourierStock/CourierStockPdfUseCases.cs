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

/// <summary>`GET /courier-stock/pdf`.</summary>
public sealed record GetCourierStockPdfQuery(ListQueryParams Query, string[]? AgencyId, string? All) : IRequest<FileContent>;

internal sealed class GetCourierStockPdfHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IFileStorage storage, IPdfService pdf) : IRequestHandler<GetCourierStockPdfQuery, FileContent>
{
    public async Task<FileContent> Handle(GetCourierStockPdfQuery request, CancellationToken cancellationToken) =>
        CourierStockPdfs.RenderSync(pdf, await CourierStockPdfs.GlobalAsync(db, caller, clock, storage, CourierStocks.ListFilter(request.Query, request.AgencyId, request.All), cancellationToken));
}

/// <summary>`GET /courier-stock/:courierId/pdf`.</summary>
public sealed record GetCourierStockCourierPdfQuery(string CourierId, ListQueryParams Query, string? StatusId, string[]? ClientId, string[]? CityId, string[]? RouteId) : IRequest<FileContent>;

internal sealed class GetCourierStockCourierPdfHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IFileStorage storage, IPdfService pdf)
    : IRequestHandler<GetCourierStockCourierPdfQuery, FileContent>
{
    public async Task<FileContent> Handle(GetCourierStockCourierPdfQuery request, CancellationToken cancellationToken) =>
        CourierStockPdfs.RenderSync(pdf, await CourierStockPdfs.IndividualAsync(db, caller, clock, storage, request.CourierId,
            CourierStocks.ParcelFilter(request.Query, request.StatusId, request.ClientId, request.CityId, request.RouteId), cancellationToken));
}

/// <summary>
/// `courierStockPdfJobSchema` (union sur `kind`) : « global » (filtres de la liste) ou « courier » (un livreur et ses
/// filtres). Le document est préparé tout de suite, rendu en arrière-plan (file des étiquettes).
/// </summary>
public sealed record CreateCourierStockPdfJobCommand(JsonElement? Body) : IRequest<LabelJobDto>;

internal sealed class CreateCourierStockPdfJobHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IFileStorage storage, LabelJobQueue queue)
    : IRequestHandler<CreateCourierStockPdfJobCommand, LabelJobDto>
{
    public async Task<LabelJobDto> Handle(CreateCourierStockPdfJobCommand request, CancellationToken cancellationToken)
    {
        var body = request.Body is { ValueKind: JsonValueKind.Object } b ? b : default;
        string? Text(string key) => body.ValueKind == JsonValueKind.Object && body.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        string[]? Many(string key) => body.ValueKind == JsonValueKind.Object && body.TryGetProperty(key, out var v)
            ? v.ValueKind == JsonValueKind.String ? [v.GetString()!] : v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray() : null
            : null;
        var kind = Text("kind");
        if (kind is not ("global" or "courier"))
        {
            throw BusinessException.Validation("Saisie invalide.", new { issues = new[] { new { path = "kind", message = "Invalid input" } } });
        }
        var query = new ListQueryParams { Search = Text("search"), Sort = Text("sort"), Order = Text("order") };
        CourierStockPdfs.Prepared prepared;
        if (kind == "global")
        {
            prepared = await CourierStockPdfs.GlobalAsync(db, caller, clock, storage, CourierStocks.ListFilter(query, Many("agencyId"), Text("all")), cancellationToken);
        }
        else
        {
            var courierId = Text("courierId")?.Trim();
            if (string.IsNullOrEmpty(courierId))
            {
                throw BusinessException.Validation("Saisie invalide.", new { issues = new[] { new { path = "courierId", message = courierId is null ? Zod.Expected("string") : Zod.TooSmallString(1) } } });
            }
            var status = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("statusId", out var s) ? s.ToString() : null;
            prepared = await CourierStockPdfs.IndividualAsync(db, caller, clock, storage, courierId,
                CourierStocks.ParcelFilter(query, status, Many("clientId"), Many("cityId"), Many("routeId")), cancellationToken);
        }
        var now = clock.UtcNow;
        var row = new LabelJob { RequestedById = caller.UserId, Format = ParcelLabels.StockJobFormat, ParcelCount = prepared.Rows, CreatedAt = now, ExpiresAt = now.AddHours(ParcelLabels.JobTtlHours) };
        db.LabelJobs.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        queue.Enqueue(new LabelJobWork(row.Id, [], row.Format, $"courier-stock/{caller.CompanyId}", prepared.Data));
        return LabelJobDto.From(row);
    }
}
