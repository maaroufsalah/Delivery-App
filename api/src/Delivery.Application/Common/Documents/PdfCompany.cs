using Delivery.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Common.Documents;

/// <summary>`PdfCompany` : l'en-tête et le pied légal des documents PDF.</summary>
public sealed record PdfCompany(string Name, string? LegalName, string? TaxId, string? Rc, string? Address, string? City, string? Phone, string? Email, string? Website, string? ShortName = null)
{
    public static PdfCompany Blank(string label) => new(label, null, null, null, null, null, null, null, null);

    /// <summary>`loadPdfCompany`.</summary>
    public static async Task<PdfCompany> LoadAsync(IAppDbContext db, string fallback, CancellationToken cancellationToken) =>
        await db.Companies.AsNoTracking().OrderBy(c => c.CreatedAt)
            .Select(c => new PdfCompany(c.Name, c.LegalName, c.TaxId, c.Rc, c.Address, c.City, c.Phone, c.Email, c.Website, c.ShortName))
            .FirstOrDefaultAsync(cancellationToken) ?? Blank(fallback);

    /// <summary>`companyTitle`.</summary>
    public string Title => Clean(LegalName) is { Length: > 0 } legal ? legal : Name.Trim() is { Length: > 0 } name ? name : "Document";

    /// <summary>`companyShortName` : le réglage, sinon le nom tronqué à 20.</summary>
    public string ShortText => Clean(ShortName) is { Length: > 0 } s ? s : Name.Trim().Length > 20 ? Name.Trim()[..20] : Name.Trim();

    /// <summary>`companyHeaderLines`.</summary>
    public IReadOnlyList<string> HeaderLines()
    {
        var lines = new List<string>();
        var where = string.Join(", ", new[] { Address, City }.Select(Clean).Where(s => s.Length > 0));
        if (where.Length > 0) lines.Add(where);
        var reach = string.Join(" · ", new[] { Phone, Email }.Select(Clean).Where(s => s.Length > 0));
        if (reach.Length > 0) lines.Add(reach);
        return lines;
    }

    /// <summary>`companyLegalLine`.</summary>
    public string LegalLine()
    {
        var parts = new List<string>();
        var legal = Clean(LegalName) is { Length: > 0 } l ? l : Clean(Name);
        if (legal.Length > 0) parts.Add(legal);
        var where = string.Join(", ", new[] { Address, City }.Select(Clean).Where(s => s.Length > 0));
        if (where.Length > 0) parts.Add(where);
        if (Clean(Phone) is { Length: > 0 } phone) parts.Add($"Tél. {phone}");
        if (Clean(TaxId) is { Length: > 0 } ice) parts.Add($"ICE {ice}");
        if (Clean(Rc) is { Length: > 0 } rc) parts.Add($"RC {rc}");
        if (Clean(Website) is { Length: > 0 } web) parts.Add(web);
        return string.Join(", ", parts);
    }

    /// <summary>`documentFooter`.</summary>
    public string Footer(string note)
    {
        var legal = LegalLine();
        if (legal.Length == 0) return note;
        if (note.Trim().Length == 0) return legal;
        return $"{note}\n{legal}";
    }

    private static string Clean(string? value) => value?.Trim() ?? string.Empty;
}

public sealed record StorageReceiptLine(string? Reference, string Name, int Quantity);

/// <summary>`StorageReceiptDocument` : le bon de réception d'une demande d'entreposage.</summary>
public sealed record StorageReceipt(PdfCompany Company, string Code, string AgencyName, string ClientName, string Status, DateTime? DecidedAt, string? DecidedByName,
    string? RefusalReason, IReadOnlyList<StorageReceiptLine> Lines, int LineCount, int TotalQuantity);
