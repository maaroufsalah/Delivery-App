namespace Delivery.Application.Common.Interfaces;

/// <summary>Horloge injectable : millisecondes tronquées, comme un `timestamptz(3)`.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

/// <summary>Fichier écrit : sa clé (`dossier/uuid.ext`), sa taille, son empreinte.</summary>
public sealed record StoredFile(string Key, long SizeBytes, string MimeType, string Checksum);

/// <summary>Image ré-encodée : pixels seulement, métadonnées (EXIF, scripts SVG) retirées.</summary>
public sealed record SanitizedImage(byte[] Content, string MimeType, int Width, int Height, string SourceFormat);

/// <summary>
/// Stockage des pièces jointes et du logo (volume `uploads`, `StorageService`),
/// et l'assainissement des images reçues (`image-sanitizer.ts`).
/// </summary>
public interface IFileStorage
{
    Task<StoredFile> PutAsync(string folder, byte[] content, string mimeType, CancellationToken cancellationToken);

    /// <summary>Contenu d'une clé ; 404 « Fichier introuvable. » si absente.</summary>
    Task<byte[]> GetAsync(string key, CancellationToken cancellationToken);

    Task RemoveAsync(string key, CancellationToken cancellationToken);

    /// <summary>Décode, vérifie le type annoncé, redimensionne (`maxSide`) et ré-encode ; 422 nommé sinon.</summary>
    SanitizedImage SanitizeImage(byte[] input, string declaredMime, int maxSide, bool preserveFormat = false);

    /// <summary>`brandIcon` : le logo centré sur un carré de la couleur de fond, en PNG.</summary>
    byte[] BrandIcon(byte[] sanitizedLogo, string? background, int size = 64);
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

    /// <summary>`writeTemplate` : en-tête sombre, ligne d'exemple, listes déroulantes (feuille cachée), feuille d'aide.</summary>
    byte[] WriteTemplate(ExcelTemplate template);
}

public sealed record ExcelTemplateColumn(string Header, string Example, IReadOnlyList<string>? Choices, bool Required, bool SuggestOnly, double? Width, bool Text);

public sealed record ExcelTemplate(string SheetName, string ListsName, int RowCount, IReadOnlyList<ExcelTemplateColumn> Columns, string HelpName, string HelpTitle, IReadOnlyList<(string Label, string Text)> HelpRows);

public sealed record ExcelTable(IReadOnlyList<string> RawHeader, IReadOnlyList<IReadOnlyList<string>> Rows, IReadOnlyList<int> RowNumbers);

/// <summary>Production des documents PDF (étiquettes, bons, factures).</summary>
public interface IPdfService
{
    byte[] Render(object document);
}
