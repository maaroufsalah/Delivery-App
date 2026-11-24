using System.Globalization;
using ClosedXML.Excel;
using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;

namespace Delivery.Infrastructure.Documents;

/// <summary>Classeurs Excel au gabarit de `SpreadsheetService` (exceljs côté Speedy).</summary>
public sealed class ExcelService : IExcelService
{
    private static readonly XLColor Muted = XLColor.FromHtml("#6B7280");

    public byte[] Write(string sheetName, IReadOnlyList<string> header, IReadOnlyList<IReadOnlyList<object?>> rows, ExcelBanner? banner, IReadOnlySet<int>? textColumns = null)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SafeName(sheetName));
        var row = 1;
        if (banner is not null) row = WriteBanner(sheet, banner);
        WriteRow(sheet, row, header.Cast<object?>().ToList(), null);
        sheet.Row(row).Style.Font.Bold = true;
        row++;
        foreach (var cells in rows)
        {
            WriteRow(sheet, row, cells, textColumns);
            row++;
        }
        if (banner is not null)
        {
            row++;
            var footer = sheet.Cell(row, 1);
            footer.Value = $"{rows.Count} {(rows.Count < 2 ? "ligne" : "lignes")}";
            footer.Style.Font.Italic = true;
            footer.Style.Font.FontSize = 9;
            footer.Style.Font.FontColor = Muted;
        }
        var columns = Math.Max(header.Count, rows.Count == 0 ? 0 : rows.Max(r => r.Count));
        for (var c = 1; c <= Math.Max(columns, 1); c++) sheet.Column(c).Width = 18;
        return Save(workbook);
    }

    public byte[] WriteSheets(IReadOnlyList<ExcelSheet> sheets)
    {
        using var workbook = new XLWorkbook();
        foreach (var spec in sheets)
        {
            var sheet = workbook.Worksheets.Add(SafeName(spec.Name));
            var row = 1;
            if (spec.Banner is not null) row = WriteBanner(sheet, spec.Banner);
            if (spec.Header is not null)
            {
                WriteRow(sheet, row, spec.Header.Cast<object?>().ToList(), null);
                sheet.Row(row).Style.Font.Bold = true;
                row++;
            }
            for (var i = 0; i < spec.Rows.Count; i++)
            {
                WriteRow(sheet, row, spec.Rows[i], spec.TextColumns);
                if (spec.AmountColumns is not null)
                {
                    foreach (var column in spec.AmountColumns) sheet.Cell(row, column + 1).Style.NumberFormat.Format = "#,##0.00";
                }
                if (spec.BoldRows?.Contains(i) == true) sheet.Row(row).Style.Font.Bold = true;
                row++;
            }
            var width = spec.Header?.Count ?? (spec.Rows.Count == 0 ? 1 : spec.Rows.Max(r => r.Count));
            for (var c = 0; c < Math.Max(width, 1); c++)
            {
                sheet.Column(c + 1).Width = spec.Widths is not null && c < spec.Widths.Count ? spec.Widths[c] : 18;
            }
        }
        return Save(workbook);
    }

    public ExcelTable Read(byte[] content)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(new MemoryStream(content));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            throw BusinessException.Validation("Fichier illisible : un classeur .xlsx est attendu.");
        }
        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault() ?? throw BusinessException.Validation("Le classeur ne contient aucune feuille.");
            var used = sheet.RangeUsed();
            if (used is null) throw BusinessException.Validation("Le classeur est vide.");
            var columnCount = used.LastColumn().ColumnNumber();
            var table = new List<(int Number, List<string> Cells)>();
            foreach (var row in sheet.RowsUsed())
            {
                var cells = new List<string>();
                for (var c = 1; c <= columnCount; c++) cells.Add(CellText(row.Cell(c)));
                table.Add((row.RowNumber(), cells));
            }
            if (table.Count == 0) throw BusinessException.Validation("Le classeur est vide.");
            var kept = table.Skip(1).Where(r => r.Cells.Any(c => c.Length > 0)).ToList();
            return new ExcelTable(table[0].Cells, kept.Select(r => (IReadOnlyList<string>)r.Cells).ToList(), kept.Select(r => r.Number).ToList());
        }
    }

    private static string CellText(IXLCell cell)
    {
        var value = cell.Value;
        if (value.IsBlank) return string.Empty;
        if (value.IsNumber) return value.GetNumber().ToString(CultureInfo.InvariantCulture);
        if (value.IsDateTime) return value.GetDateTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        if (value.IsBoolean) return value.GetBoolean() ? "true" : "false";
        return value.ToString(CultureInfo.InvariantCulture).Trim();
    }

    private static int WriteBanner(IXLWorksheet sheet, ExcelBanner banner)
    {
        var title = sheet.Cell(1, 1);
        title.Value = banner.Title;
        title.Style.Font.Bold = true;
        title.Style.Font.FontSize = 13;
        var row = 2;
        if (!string.IsNullOrWhiteSpace(banner.Subtitle))
        {
            var subtitle = sheet.Cell(2, 1);
            subtitle.Value = banner.Subtitle;
            subtitle.Style.Font.FontSize = 9;
            subtitle.Style.Font.FontColor = Muted;
            row = 3;
        }
        return row + 1;
    }

    private static void WriteRow(IXLWorksheet sheet, int row, IReadOnlyList<object?> cells, IReadOnlySet<int>? textColumns)
    {
        for (var c = 0; c < cells.Count; c++)
        {
            var cell = sheet.Cell(row, c + 1);
            switch (cells[c])
            {
                case null:
                    break;
                case string text:
                    cell.Value = text;
                    if (textColumns?.Contains(c) == true) cell.Style.NumberFormat.Format = "@";
                    break;
                case int i:
                    cell.Value = i;
                    break;
                case long l:
                    cell.Value = l;
                    break;
                case decimal d:
                    cell.Value = d;
                    break;
                case double d:
                    cell.Value = d;
                    break;
                default:
                    cell.Value = Convert.ToString(cells[c], CultureInfo.InvariantCulture);
                    break;
            }
        }
    }

    private static string SafeName(string name) => name.Length > 31 ? name[..31] : name;

    private static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] WriteTemplate(ExcelTemplate template)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(template.SheetName);
        var lists = workbook.Worksheets.Add(template.ListsName);
        lists.Visibility = XLWorksheetVisibility.VeryHidden;
        for (var index = 0; index < template.Columns.Count; index++)
        {
            var column = template.Columns[index];
            var position = index + 1;
            var head = sheet.Cell(1, position);
            head.Value = column.Header;
            head.Style.Font.Bold = true;
            head.Style.Font.FontColor = XLColor.White;
            head.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F1F1F");
            var example = sheet.Cell(2, position);
            example.Value = column.Example;
            example.Style.Font.Italic = true;
            example.Style.Font.FontColor = XLColor.FromHtml("#808080");
            sheet.Column(position).Width = column.Width ?? Math.Max(14, column.Header.Length + 4);
            if (column.Text) sheet.Column(position).Style.NumberFormat.Format = "@";
            if (column.Choices is not { Count: > 0 } choices) continue;
            lists.Cell(1, position).Value = column.Header;
            for (var i = 0; i < choices.Count; i++) lists.Cell(i + 2, position).Value = choices[i];
            var letter = XLHelper.GetColumnLetterFromNumber(position);
            var validation = sheet.Range(2, position, template.RowCount + 1, position).CreateDataValidation();
            validation.List($"'{template.ListsName}'!${letter}$2:${letter}${choices.Count + 1}", true);
            validation.IgnoreBlanks = !column.Required;
            validation.ShowErrorMessage = !column.SuggestOnly;
            validation.ErrorStyle = XLErrorStyle.Warning;
            validation.ErrorMessage = "Valeur hors liste. Choisissez une valeur proposée.";
        }
        sheet.Row(1).Height = 22;
        var help = workbook.Worksheets.Add(template.HelpName);
        help.Column(1).Width = 28;
        help.Column(2).Width = 90;
        help.Cell(1, 1).Value = template.HelpTitle;
        help.Cell(1, 1).Style.Font.Bold = true;
        help.Cell(1, 1).Style.Font.FontSize = 14;
        var row = 3;
        foreach (var (label, text) in template.HelpRows)
        {
            help.Cell(row, 1).Value = label;
            help.Cell(row, 1).Style.Font.Bold = true;
            help.Cell(row, 2).Value = text;
            help.Cell(row, 2).Style.Alignment.WrapText = true;
            help.Cell(row, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            row++;
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
