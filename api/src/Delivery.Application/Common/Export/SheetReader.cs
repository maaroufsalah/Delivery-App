using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;

namespace Delivery.Application.Common.Export;

/// <summary>`SpreadsheetService.read` : un classeur reçu en base64, 4 Mo au plus.</summary>
public static class SheetReader
{
    public const int MaxBase64Length = 4_000_000;

    public static ExcelTable Read(IExcelService excel, string base64)
    {
        if (base64.Length > MaxBase64Length) throw BusinessException.Validation("Fichier trop volumineux (4 Mo maximum).");
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            // `Buffer.from(…, 'base64')` ignore les caractères parasites : un contenu illisible échoue à la lecture du classeur.
            throw BusinessException.Validation("Fichier illisible : un classeur .xlsx est attendu.");
        }
        return excel.Read(bytes);
    }
}
