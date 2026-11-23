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

/// <summary>Production des classeurs Excel.</summary>
public interface IExcelService
{
    byte[] Build(string sheetName, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows);
}

/// <summary>Production des documents PDF (étiquettes, bons, factures).</summary>
public interface IPdfService
{
    byte[] Render(object document);
}
