namespace Delivery.Domain.Common;

/// <summary>
/// Document historique repris de Wimo Laravel (`ImportedAt` renseigné) : ses montants sont ceux d'origine, il ne se
/// modifie pas dans .NET. Un document ouvert dans Laravel (brouillon, caisse non reçue) est repris sans ce marquage.
/// </summary>
public static class ImportedDocument
{
    public const string Reason = "imported-document-locked";

    public static void EnsureEditable(DateTime? importedAt, string what)
    {
        if (importedAt is not null)
        {
            throw BusinessException.Conflict($"{what} est repris de Wimo Laravel : il ne se modifie pas.", new { reason = Reason });
        }
    }
}
