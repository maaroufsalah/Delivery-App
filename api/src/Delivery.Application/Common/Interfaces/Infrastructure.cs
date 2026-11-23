namespace Delivery.Application.Common.Interfaces;

/// <summary>Horloge injectable : millisecondes tronquées, comme un `timestamptz(3)`.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

/// <summary>Stockage des pièces jointes et du logo (volume `uploads`).</summary>
public interface IFileStorage
{
    Task<string> SaveAsync(string folder, string fileName, Stream content, string contentType, CancellationToken cancellationToken);

    Task<Stream?> OpenAsync(string key, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

/// <summary>Bandeau d'un export : raison sociale, puis ligne légale.</summary>
public sealed record ExcelBanner(string Title, string? Subtitle);

/// <summary>Une feuille de classeur (`ExportSheet`).</summary>
public sealed record ExcelSheet(
    string Name,
    IReadOnlyList<string>? Header,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    ExcelBanner? Banner = null,
    IReadOnlyList<double>? Widths = null,
    IReadOnlySet<int>? TextColumns = null,
    IReadOnlySet<int>? AmountColumns = null,
    IReadOnlySet<int>? BoldRows = null);

/// <summary>Lecture et écriture des classeurs Excel (`SpreadsheetService`).</summary>
public interface IExcelService
{
    /// <summary>`write` : bandeau, en-tête gras, lignes, pied « n lignes », colonnes de 18.</summary>
    byte[] Write(string sheetName, IReadOnlyList<string> header, IReadOnlyList<IReadOnlyList<object?>> rows, ExcelBanner? banner, IReadOnlySet<int>? textColumns = null);

    byte[] WriteSheets(IReadOnlyList<ExcelSheet> sheets);

    /// <summary>Première feuille en texte : en-tête brut puis lignes non vides.</summary>
    ExcelTable Read(byte[] content);
}

public sealed record ExcelTable(IReadOnlyList<string> RawHeader, IReadOnlyList<IReadOnlyList<string>> Rows, IReadOnlyList<int> RowNumbers);

/// <summary>Production des documents PDF (étiquettes, bons, factures).</summary>
public interface IPdfService
{
    byte[] Render(object document);
}
