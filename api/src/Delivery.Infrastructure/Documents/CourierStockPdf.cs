using System.Globalization;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Domain.Parcels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Delivery.Infrastructure.Documents;

/// <summary>`courier-stock.tsx` : mêmes colonnes (largeurs en points), blocs de 100 lignes, pied et numéro de page.</summary>
internal static class CourierStockPdf
{
    private const string Ink = "#111111";
    private const string Muted = "#6B6B6B";
    private const int RowsPerBlock = 100;
    private static readonly PageSize A4 = new(595.28f, 841.89f);

    public static byte[] Render(CourierStockSheet data)
    {
        var generated = Exports.DateTimeText(data.ProducedAt);
        var footer = data.Company.Footer($"{CourierStockSheet.Title} au {generated} — colis et argent détenus par le livreur à cet instant.");
        return Document.Create(container =>
        {
            if (data.Summary is { } summary)
            {
                Page(container, data, "Récapitulatif par livreur", generated, footer, column =>
                {
                    if (summary.Filters.Count > 0) column.Item().PaddingBottom(6).Text($"Filtres : {string.Join(" · ", summary.Filters)}").FontColor(Muted);
                    column.Item().Element(TableHead).Row(head =>
                    {
                        Caption(head.ConstantItem(104), "Livreur");
                        Caption(head.ConstantItem(62), "Agence");
                        foreach (var label in new[] { "En main", "Ramas.", "Attente", "En cours", "Échec", "Livrés" }) Caption(head.ConstantItem(34).AlignRight(), label);
                        Caption(head.ConstantItem(62).AlignRight(), "Encaissé");
                        Caption(head.ConstantItem(62).AlignRight(), "À encaisser");
                    });
                    foreach (var row in summary.Rows) SummaryLine(column, row, total: false);
                    SummaryLine(column, new CourierStockSheetRow("Total", null, summary.Totals), total: true);
                    column.Item().PaddingTop(6).Text("Ramas. = ramassés non validés · Échec = échecs et retours en main · Livrés = livrés, COD non remis · Encaissé = COD encaissé non remis.")
                        .FontSize(7).FontColor(Muted);
                });
            }
            foreach (var section in data.Sections)
            {
                var blocks = section.Parcels.Count == 0 ? [[]] : section.Parcels.Chunk(RowsPerBlock).ToList();
                for (var part = 0; part < blocks.Count; part++)
                {
                    var parcels = blocks[part];
                    var first = part == 0;
                    Page(container, data, first ? section.CourierName : $"{section.CourierName} (suite)", generated, footer, column =>
                    {
                        if (first) SectionHead(column, section);
                        column.Item().PaddingTop(8).Element(TableHead).Row(head =>
                        {
                            Caption(head.ConstantItem(92), "Colis");
                            Caption(head.RelativeItem(), "Destinataire");
                            Caption(head.ConstantItem(64), "Ville");
                            Caption(head.ConstantItem(70), "Vendeur");
                            Caption(head.ConstantItem(74), "Statut");
                            Caption(head.ConstantItem(64), "Circuit");
                            Caption(head.ConstantItem(56).AlignRight(), "COD");
                        });
                        foreach (var parcel in parcels)
                        {
                            column.Item().ShowEntire().Element(TableRow).DefaultTextStyle(s => s.FontSize(7.5f)).Row(row =>
                            {
                                row.ConstantItem(92).Text(parcel.Code).Bold();
                                row.RelativeItem().Column(who =>
                                {
                                    who.Item().Text(PdfText.Truncate(parcel.RecipientName, 30));
                                    who.Item().Text(parcel.RecipientPhone).FontColor(Muted);
                                });
                                row.ConstantItem(64).Text(PdfText.Truncate(parcel.DestinationCityName, 14));
                                row.ConstantItem(70).Text(PdfText.Truncate(parcel.ClientName, 16));
                                row.ConstantItem(74).Text(PdfText.Truncate(DocumentLabels.ParcelStatus(parcel.StatusId), 22));
                                row.ConstantItem(64).Text(parcel.RouteCode ?? "—");
                                row.ConstantItem(56).AlignRight().Text(PdfText.Amount(parcel.CodInCents));
                            });
                        }
                        if (section.Parcels.Count == 0) column.Item().PaddingTop(10).Text("Aucun colis en stock.").FontColor(Muted);
                    });
                }
            }
        }).WithMetadata(new DocumentMetadata { Title = CourierStockSheet.Title }).GeneratePdf();
    }

    private static IContainer TableHead(IContainer container) => container.Background("#F1F1F1").PaddingVertical(5).PaddingHorizontal(6);

    private static IContainer TableRow(IContainer container) => container.BorderBottom(0.4f).BorderColor(BusinessDocuments.RowRule).PaddingVertical(5).PaddingHorizontal(6);

    private static void Caption(IContainer container, string text) => BusinessDocuments.Caption(container, text);

    private static void Page(IDocumentContainer container, CourierStockSheet data, string subtitle, string generated, string footer, Action<ColumnDescriptor> body)
    {
        container.Page(page =>
        {
            page.Size(A4);
            page.MarginTop(32);
            page.MarginBottom(8);
            page.MarginHorizontal(34);
            page.DefaultTextStyle(PdfService.Base(9).FontColor(Ink));
            page.Content().PaddingBottom(12).Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(head =>
                    {
                        if (data.Logo is { Length: > 0 } logo) head.Item().PaddingBottom(4).Height(32).MaxWidth(150).AlignLeft().Image(logo).FitArea();
                        head.Item().Text(PdfText.Truncate(string.IsNullOrWhiteSpace(data.ShortName) ? data.Company.Title : data.ShortName.Trim(), 34)).FontSize(15).Bold();
                        foreach (var line in data.Company.HeaderLines()) head.Item().Text(PdfText.Truncate(line, 52)).FontColor(Muted);
                    });
                    row.AutoItem().Column(right =>
                    {
                        right.Item().AlignRight().Text(CourierStockSheet.Title).FontSize(14).Bold().LetterSpacing(0.04f);
                        right.Item().AlignRight().Text(subtitle);
                        right.Item().AlignRight().Text($"Généré le {generated}").FontColor(Muted);
                    });
                });
                BusinessDocuments.Rule(column);
                body(column);
            });
            // Pied (`styles.footer`, 20 pt du bas) et numéro de page (8 pt du bas, à droite).
            page.Footer().Column(foot =>
            {
                foot.Item().Text(footer).FontSize(6.5f).FontColor("#8A8A8A");
                foot.Item().PaddingTop(1).AlignRight().Text(text =>
                {
                    text.DefaultTextStyle(s => s.FontSize(6.5f).FontColor("#8A8A8A"));
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        });
    }

    private static void SummaryLine(ColumnDescriptor column, CourierStockSheetRow row, bool total)
    {
        var c = row.Counters;
        column.Item().ShowEntire().Element(TableRow).DefaultTextStyle(s => total ? s.FontSize(7.5f).Bold() : s.FontSize(7.5f)).Row(line =>
        {
            line.ConstantItem(104).Text(PdfText.Truncate(row.CourierName, 24));
            line.ConstantItem(62).Text(PdfText.Truncate(row.AgencyName ?? string.Empty, 14));
            line.ConstantItem(34).AlignRight().Text(Count(c.InHand)).Bold();
            foreach (var value in new[] { c.PickedUp, c.Awaiting, c.OutForDelivery, c.FailedOrReturning, c.DeliveredUnremitted }) line.ConstantItem(34).AlignRight().Text(Count(value));
            line.ConstantItem(62).AlignRight().Text(PdfText.Amount(c.CollectedUnremittedInCents));
            line.ConstantItem(62).AlignRight().Text(PdfText.Amount(c.ToCollectInCents));
        });
    }

    private static void SectionHead(ColumnDescriptor column, CourierStockSheetSection section)
    {
        var c = section.Counters;
        column.Item().ShowEntire().Column(head =>
        {
            Fields(head, [("Livreur", section.CourierName), ("Agence", section.AgencyName ?? "—"), ("Colis en main", Count(c.InHand)), ("Livrés non remis", Count(c.DeliveredUnremitted))], 0);
            Fields(head, [("Ramassés non validés", Count(c.PickedUp)), ("En attente / affectés", Count(c.Awaiting)), ("En cours de livraison", Count(c.OutForDelivery)),
                ("Échec / à retourner", Count(c.FailedOrReturning))], 6);
            head.Item().PaddingTop(8).Border(0.6f).BorderColor(Ink).CornerRadius(3).Padding(8).Row(box =>
            {
                box.AutoItem().Text($"COD encaissé non remis : {PdfText.Amount(c.CollectedUnremittedInCents)}").Bold();
                box.RelativeItem();
                box.AutoItem().Text($"COD à encaisser : {PdfText.Amount(c.ToCollectInCents)}");
            });
            if (section.ByStatus.Count > 0)
            {
                head.Item().PaddingTop(8).Column(lines =>
                {
                    BusinessDocuments.Caption(lines.Item(), "Récapitulatif par statut");
                    foreach (var line in section.ByStatus)
                    {
                        lines.Item().BorderBottom(0.4f).BorderColor("#CFCFCF").PaddingVertical(2).DefaultTextStyle(s => s.FontSize(7.5f)).Row(row =>
                        {
                            row.RelativeItem().Text(DocumentLabels.ParcelStatus(line.StatusId));
                            row.ConstantItem(34).AlignRight().Text(Count(line.Count));
                            row.ConstantItem(62).AlignRight().Text(PdfText.Amount(line.CodInCents));
                        });
                    }
                });
            }
        });
    }

    private static void Fields(ColumnDescriptor column, IReadOnlyList<(string Label, string Value)> fields, float top) => column.Item().PaddingTop(top).Row(row =>
    {
        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0) row.RelativeItem();
            var (label, value) = fields[i];
            row.ConstantItem(120).Column(f =>
            {
                BusinessDocuments.Caption(f.Item(), label);
                f.Item().PaddingTop(2).Text(PdfText.Truncate(value, 26));
            });
        }
    });

    private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);
}
