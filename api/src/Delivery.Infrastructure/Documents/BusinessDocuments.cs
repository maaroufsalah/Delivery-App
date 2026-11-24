using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Delivery.Infrastructure.Documents;

/// <summary>Documents A4 de gestion (`styles.ts` : page, titre, légende, tableau, pied légal).</summary>
internal static class BusinessDocuments
{
    private const string Ink = "#111111";
    private const string Muted = "#6B6B6B";
    private static readonly PageSize A4 = new(595.28f, 841.89f);

    internal static void Page(IDocumentContainer container, PdfCompany company, string footerNote, Action<ColumnDescriptor> body)
    {
        container.Page(page =>
        {
            page.Size(A4);
            page.MarginTop(32);
            page.MarginBottom(40);
            page.MarginHorizontal(34);
            page.DefaultTextStyle(PdfService.Base(9).FontColor(Ink));
            page.Content().Column(body);
            page.Footer().AlignCenter().Text(company.Footer(footerNote)).FontSize(7).FontColor(Muted);
        });
    }

    internal static TextBlockDescriptor Caption(IContainer container, string text) => container.Text(text.ToUpperInvariant()).FontSize(7).FontColor(Muted).LetterSpacing(0.08f);

    internal static void Rule(ColumnDescriptor column) => column.Item().PaddingVertical(10).LineHorizontal(0.6f).LineColor(Ink);

    public static byte[] StorageReceipt(StorageReceipt receipt)
    {
        var statusLabel = receipt.Status switch
        {
            "pending" => "En attente de validation",
            "validated" => "Validée, marchandise entrée",
            "refused" => "Refusée",
            _ => receipt.Status,
        };
        return Document.Create(container => Page(container, receipt.Company, "Les quantités portées sur ce bon sont celles entrées en stock à la validation.", column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(head =>
                {
                    head.Item().Text(PdfText.Truncate(receipt.Company.Title, 34)).FontSize(15).Bold();
                    head.Item().PaddingTop(2).Text("Bon de réception, entreposage").FontColor(Muted);
                    foreach (var line in receipt.Company.HeaderLines()) head.Item().Text(PdfText.Truncate(line, 52)).FontColor(Muted);
                });
                row.AutoItem().AlignRight().Column(code =>
                {
                    code.Item().AlignRight().Text(receipt.Code).FontSize(14).Bold().LetterSpacing(0.04f);
                    code.Item().AlignRight().Text($"Agence : {PdfText.Truncate(receipt.AgencyName, 24)}").FontColor(Muted);
                    code.Item().AlignRight().PaddingTop(4).Width(62).Height(62).Image(PdfService.Qr(receipt.Code));
                });
            });
            Rule(column);
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    Caption(c.Item(), "E-commerçant");
                    c.Item().PaddingTop(2).Text(receipt.ClientName).FontSize(11);
                });
                row.RelativeItem().Column(c =>
                {
                    Caption(c.Item(), "État");
                    c.Item().PaddingTop(2).Text(statusLabel).FontSize(11);
                    if (receipt.DecidedAt is { } decided) c.Item().Text($"Le {Exports.DateText(decided)}{(receipt.DecidedByName is null ? string.Empty : $", {receipt.DecidedByName}")}").FontColor(Muted);
                });
            });
            if (receipt.Status == "refused" && !string.IsNullOrEmpty(receipt.RefusalReason))
            {
                column.Item().PaddingTop(12).Border(0.6f).BorderColor(Ink).Padding(8).Column(c =>
                {
                    Caption(c.Item(), "Motif du refus");
                    c.Item().PaddingTop(2).Text(receipt.RefusalReason);
                });
            }
            column.Item().PaddingTop(16).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(140);
                    columns.RelativeColumn();
                    columns.ConstantColumn(70);
                });
                table.Header(header =>
                {
                    Caption(header.Cell().Background("#F1F1F1").PaddingVertical(5).PaddingHorizontal(6), "Référence");
                    Caption(header.Cell().Background("#F1F1F1").PaddingVertical(5).PaddingHorizontal(6), "Produit");
                    Caption(header.Cell().Background("#F1F1F1").PaddingVertical(5).PaddingHorizontal(6).AlignRight(), "Quantité");
                });
                foreach (var line in receipt.Lines)
                {
                    IContainer Cell(IContainer c) => c.BorderBottom(0.4f).BorderColor("#CFCFCF").PaddingVertical(5).PaddingHorizontal(6);
                    table.Cell().Element(Cell).Text(line.Reference ?? "-");
                    table.Cell().Element(Cell).Text(PdfText.Truncate(line.Name, 42));
                    table.Cell().Element(Cell).AlignRight().Text(line.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            });
            column.Item().PaddingTop(12).Row(row =>
            {
                row.RelativeItem().Text($"{receipt.LineCount} référence(s)").Bold();
                row.RelativeItem().AlignRight().Text($"Total : {receipt.TotalQuantity} unité(s)").Bold();
            });
            column.Item().PaddingTop(36).Row(row =>
            {
                foreach (var label in new[] { "Déposé par", "Reçu par (agence)" })
                {
                    row.ConstantItem(210).Column(c =>
                    {
                        Caption(c.Item(), label);
                        c.Item().PaddingTop(30).LineHorizontal(0.6f).LineColor(Ink);
                    });
                    row.RelativeItem();
                }
            });
        })).WithMetadata(new DocumentMetadata { Title = receipt.Code }).GeneratePdf();
    }
}
