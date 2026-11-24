using System.Globalization;
using System.Text;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Models;
using Delivery.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Common.Export;

public sealed record ExportColumn<T>(string Header, Func<T, object?> Value, bool Text = false);

/// <summary>`ExportService` : le même filtre que l'écran, en classeur, 50 000 lignes au plus.</summary>
public static class Exports
{
    public const int MaxRows = 50_000;

    public static readonly TimeZoneInfo Casablanca = TimeZoneInfo.FindSystemTimeZoneById("Africa/Casablanca");

    private static readonly CultureInfo FrMa = CultureInfo.GetCultureInfo("fr-MA");

    public static void AssertWithinLimit(int total)
    {
        if (total > MaxRows)
        {
            throw BusinessException.Validation(
                $"L'export est limité à {MaxRows.ToString("N0", FrMa)} lignes ; la sélection en compte {total.ToString("N0", FrMa)}. Affinez les filtres avant d’exporter.",
                new { limit = MaxRows, total });
        }
    }

    public static async Task<FileContent> ToXlsxAsync<T>(
        IAppDbContext db, IExcelService excel, IClock clock, ICurrentUser reader, string sheetName, IReadOnlyList<ExportColumn<T>> columns, IReadOnlyList<T> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count > MaxRows)
        {
            throw BusinessException.Validation($"L'export est limité à {MaxRows.ToString("N0", FrMa)} lignes.", new { limit = MaxRows, total = rows.Count });
        }
        var banner = await BannerAsync(db, cancellationToken);
        var text = columns.Select((c, i) => (c, i)).Where(x => x.c.Text).Select(x => x.i).ToHashSet();
        var content = excel.Write(
            sheetName,
            columns.Select(c => c.Header).ToList(),
            await CellsAsync(db, reader, columns, rows, cancellationToken),
            banner,
            text.Count > 0 ? text : null);
        return new FileContent(content, FileContent.Xlsx, $"{Slug(sheetName)}-{Stamp(clock.UtcNow)}.xlsx", CacheControl: "private, no-store");
    }

    public static async Task<ExcelBanner> BannerAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var company = await db.Companies.AsNoTracking().OrderBy(c => c.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        if (company is null) return new ExcelBanner("Document", null);
        var legal = company.LegalName?.Trim();
        var title = !string.IsNullOrEmpty(legal) ? legal : (company.Name.Trim() == string.Empty ? "Document" : company.Name.Trim());
        return new ExcelBanner(title, LegalLine(company));
    }

    public static string LegalLine(Domain.Entities.Company company)
    {
        static string Clean(string? v) => v?.Trim() ?? string.Empty;
        var parts = new List<string>();
        var legal = Clean(company.LegalName) is { Length: > 0 } l ? l : Clean(company.Name);
        if (legal.Length > 0) parts.Add(legal);
        var where = string.Join(", ", new[] { company.Address, company.City }.Select(Clean).Where(s => s.Length > 0));
        if (where.Length > 0) parts.Add(where);
        if (Clean(company.Phone) is { Length: > 0 } phone) parts.Add($"Tél. {phone}");
        if (Clean(company.TaxId) is { Length: > 0 } ice) parts.Add($"ICE {ice}");
        if (Clean(company.Rc) is { Length: > 0 } rc) parts.Add($"RC {rc}");
        if (Clean(company.Website) is { Length: > 0 } site) parts.Add(site);
        return string.Join(", ", parts);
    }

    public static string Bool(bool? value) => value == true ? "Oui" : "Non";

    /// <summary>`exportDateTime` : `JJ/MM/AAAA HH:mm` à l'heure de Casablanca.</summary>
    public static string DateTimeText(DateTime? value) => value is null ? string.Empty : Local(value.Value).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public static string DateText(DateTime? value) => value is null ? string.Empty : Local(value.Value).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string CalendarDateText(DateOnly? value) => value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>
    /// Lot O, D-228 : un vendeur lit « Livreur &lt;nom court&gt; » dans son Excel comme à l'écran ; les cellules de
    /// texte passent par le masque des livreurs quand le lecteur n'en voit pas l'identité.
    /// </summary>
    private static async Task<List<IReadOnlyList<object?>>> CellsAsync<T>(IAppDbContext db, ICurrentUser reader, IReadOnlyList<ExportColumn<T>> columns, IReadOnlyList<T> rows,
        CancellationToken cancellationToken)
    {
        var cells = rows.Select(r => (IReadOnlyList<object?>)columns.Select(c => c.Value(r)).ToList()).ToList();
        if (!reader.IsAuthenticated || reader.CanSeeCourierIdentity()) return cells;
        var mask = await CourierMasks.LoadAsync(db, reader, cancellationToken);
        return cells.Select(row => (IReadOnlyList<object?>)row.Select(cell => cell is string text ? CourierMasks.MaskText(text, mask) : cell).ToList()).ToList();
    }

    /// <summary>`exportAmount` : centimes → dirhams.</summary>
    public static decimal Amount(long? cents) => (cents ?? 0) / 100m;

    public static DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Casablanca);

    public static string Slug(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var ch in decomposed)
        {
            if (ch is >= '̀' and <= 'ͯ') continue;
            builder.Append(char.ToLowerInvariant(ch));
        }
        var slug = System.Text.RegularExpressions.Regex.Replace(builder.ToString(), "[^a-z0-9]+", "-");
        return slug.Trim('-');
    }

    /// <summary>`stamp()` : date du serveur (UTC dans le conteneur Speedy).</summary>
    public static string Stamp(DateTime utc) => utc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
}
