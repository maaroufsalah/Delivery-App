using Delivery.Application.Common.Documents;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Delivery.Infrastructure.Documents;

/// <summary>`parcel-label.tsx` (classique) et `parcel-label-branded.tsx` (à la marque).</summary>
internal static class LabelDocuments
{
    private const float LabelSide = 283.46f;
    private static readonly PageSize Label10 = new(LabelSide, LabelSide);
    private static readonly PageSize A4 = new(595.28f, 841.89f);
    private const string Muted = "#6B6B6B";

    public static byte[] Render(LabelSheet sheet)
    {
        var parcels = sheet.Expanded();
        var title = parcels.Select(p => p.Code).Distinct().Count() == 1 ? parcels[0].Code : "Étiquettes";
        var document = Document.Create(container =>
        {
            if (sheet.Template == LabelSheet.Branded && sheet.Brand is { } brand) Branded(container, parcels, brand, sheet.Format);
            else Classic(container, parcels, sheet.CompanyName, sheet.Format);
        }).WithMetadata(new DocumentMetadata { Title = title });
        return document.GeneratePdf();
    }

    private static void Classic(IDocumentContainer container, IReadOnlyList<LabelData> parcels, string company, string format)
    {
        if (format == LabelSheet.Format10X10)
        {
            foreach (var parcel in parcels)
            {
                container.Page(page =>
                {
                    page.Size(Label10);
                    page.Margin(10);
                    page.DefaultTextStyle(PdfService.Base(8));
                    page.Content().ScaleToFit().Element(c => ClassicBody(c, parcel, company, compact: true));
                });
            }
            return;
        }
        foreach (var group in parcels.Chunk(4))
        {
            container.Page(page =>
            {
                page.Size(A4);
                page.MarginVertical(32);
                page.MarginHorizontal(34);
                page.DefaultTextStyle(PdfService.Base(9).FontColor("#111111"));
                page.Content().Column(column =>
                {
                    foreach (var pair in group.Chunk(2))
                    {
                        column.Item().Height(360).Row(row =>
                        {
                            foreach (var parcel in pair)
                            {
                                row.RelativeItem().Border(0.5f).BorderColor("#BDBDBD").Padding(10).ScaleToFit()
                                    .Element(c => ClassicBody(c, parcel, company, compact: false));
                            }
                            if (pair.Length == 1) row.RelativeItem();
                        });
                    }
                });
            });
        }
    }

    private static void ClassicBody(IContainer container, LabelData parcel, string company, bool compact)
    {
        var carton = PdfText.CartonMention(parcel);
        var qrSize = compact ? 78f : 72f;
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(head =>
                {
                    head.Item().Text(PdfText.Truncate(company, 26)).FontSize(9).Bold();
                    head.Item().PaddingTop(2).Text(parcel.Code).FontSize(12).Bold().LetterSpacing(0.05f);
                    if (carton is not null) head.Item().PaddingTop(2).Text($"Carton {carton}").FontSize(10).Bold();
                });
                row.ConstantItem(qrSize).Height(qrSize).Image(PdfService.Qr(parcel.Code));
            });
            column.Item().PaddingVertical(10).LineHorizontal(0.6f).LineColor("#111111");
            column.Item().Text("DESTINATAIRE").FontSize(7).FontColor(Muted).LetterSpacing(0.08f);
            column.Item().PaddingTop(2).Text(parcel.RecipientName).FontSize(11).Bold();
            column.Item().PaddingTop(1).Text(parcel.RecipientPhone).FontSize(10);
            column.Item().PaddingTop(3).Text(PdfText.Truncate(parcel.DeliveryAddress, 80));
            column.Item().PaddingTop(2).Text(string.Join(" · ", new[] { parcel.DestinationDistrictName, parcel.DestinationCityName }.Where(s => !string.IsNullOrEmpty(s)))).Bold();
            column.Item().PaddingVertical(6).LineHorizontal(0.4f).LineColor("#CFCFCF");
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(sender =>
                {
                    sender.Item().Text("EXPÉDITEUR").FontSize(7).FontColor(Muted).LetterSpacing(0.08f);
                    sender.Item().Text(PdfText.Truncate(parcel.ClientName, 24));
                });
                row.AutoItem().Column(cod =>
                {
                    cod.Item().AlignRight().Text("À ENCAISSER").FontSize(7).FontColor(Muted).LetterSpacing(0.08f);
                    cod.Item().AlignRight().Text(PdfText.Amount(parcel.CodToCollect)).FontSize(compact ? 16 : 15).Bold();
                    if (parcel.ExpectedPaymentLabel is not null) cod.Item().AlignRight().Text(parcel.ExpectedPaymentLabel).FontSize(8).Bold();
                    if (carton is not null) cod.Item().AlignRight().Text($"Carton {carton}").FontSize(8).Bold();
                });
            });
            if (!string.IsNullOrEmpty(parcel.Description)) column.Item().PaddingTop(4).Text(PdfText.Truncate(parcel.Description, 60)).FontSize(7).FontColor(Muted);
            if (!string.IsNullOrEmpty(parcel.ExternalCode))
            {
                column.Item().PaddingTop(4).AlignCenter().Width(150).Height(18).Svg(PdfService.Barcode(parcel.ExternalCode));
                column.Item().AlignCenter().Text($"Réf. {PdfText.Truncate(parcel.ExternalCode, 32)}").FontSize(7).LetterSpacing(0.03f);
            }
        });
    }

    private static void Branded(IDocumentContainer container, IReadOnlyList<LabelData> parcels, LabelBrand brand, string format)
    {
        var a4 = format != LabelSheet.Format10X10;
        foreach (var parcel in parcels)
        {
            container.Page(page =>
            {
                page.Size(a4 ? A4 : Label10);
                page.Margin(a4 ? 40 : 10);
                page.DefaultTextStyle(PdfService.Base(8));
                var unit = a4 ? A4.Width - 80 : LabelSide - 20;
                page.Content().AlignCenter().Width(unit).Height(unit).ScaleToFit().Element(c => BrandedBody(c, parcel, brand, unit));
            });
        }
    }

    private static void BrandedBody(IContainer container, LabelData parcel, LabelBrand brand, float unit)
    {
        var k = unit / 263f;
        float Pt(float value) => MathF.Round(value * k * 10) / 10;
        var tint = PdfService.Tint(brand.Color);
        var carton = PdfText.CartonMention(parcel);
        var side = Pt(74);
        IContainer Band(IContainer c) => c.Border(0.7f).BorderColor(Colors.Black).Background(tint);
        IContainer Frame(IContainer c) => c.Border(0.7f).BorderColor(Colors.Black);

        container.Column(column =>
        {
            column.Spacing(Pt(2.5f));
            column.Item().Element(Band).Height(Pt(30)).PaddingHorizontal(Pt(8)).AlignCenter().AlignMiddle().Element(logo =>
            {
                if (brand.Logo is { Length: > 0 } bytes) logo.Height(Pt(22)).Image(bytes).FitHeight();
                else logo.Text(PdfText.Truncate(brand.Name, 22)).FontSize(Pt(14)).Bold();
            });
            column.Item().Row(row =>
            {
                Cell(row.RelativeItem(3), Pt, parcel.AgencyCode, tint, bordered: true);
                Cell(row.RelativeItem(4), Pt, parcel.ZoneName, null, bordered: true);
                Cell(row.RelativeItem(3), Pt, parcel.DestinationCityCode, tint, bordered: true);
            });
            column.Item().Row(row =>
            {
                row.Spacing(Pt(3));
                row.RelativeItem().Column(left =>
                {
                    left.Spacing(Pt(2.5f));
                    left.Item().Element(Band).PaddingVertical(Pt(2)).PaddingHorizontal(Pt(4)).Column(city =>
                    {
                        if (!string.IsNullOrEmpty(parcel.DestinationCityNameAr)) city.Item().AlignCenter().Text(parcel.DestinationCityNameAr).FontSize(Pt(13)).Bold().ClampLines(1);
                        city.Item().AlignCenter().Text(parcel.DestinationCityName.ToUpperInvariant()).FontSize(Pt(11)).Bold().ClampLines(1);
                    });
                    left.Item().Element(Frame).PaddingVertical(Pt(2.5f)).PaddingHorizontal(Pt(4)).Column(sender =>
                    {
                        sender.Item().Text($"Expéditeur : {PdfText.Truncate(parcel.ClientName, 30)}").FontSize(Pt(7)).Bold().ClampLines(1);
                        sender.Item().Text($"Téléphone : {parcel.ClientPhone}").FontSize(Pt(7)).ClampLines(1);
                        sender.Item().Text($"Date : {parcel.CreatedAt}").FontSize(Pt(6.5f)).ClampLines(1);
                    });
                });
                row.ConstantItem(side).Element(Frame).AlignCenter().AlignMiddle().Width(Pt(64)).Height(Pt(64)).Image(PdfService.Qr(parcel.Code));
            });
            column.Item().Row(row =>
            {
                row.Spacing(Pt(3));
                row.RelativeItem().Element(Frame).PaddingVertical(Pt(2.5f)).PaddingHorizontal(Pt(4)).Column(fields =>
                {
                    fields.Spacing(Pt(1));
                    Field(fields, Pt, "Destinataire", parcel.RecipientName, strong: true);
                    Field(fields, Pt, "Téléphone", parcel.RecipientPhone, strong: true, lines: parcel.RecipientPhone.Contains('/') ? 2 : 1);
                    Field(fields, Pt, "Ville", parcel.DestinationLocality);
                    Field(fields, Pt, "Adresse", PdfText.Truncate(parcel.DeliveryAddress, 160), lines: 2);
                    if (!string.IsNullOrEmpty(parcel.Comment)) Field(fields, Pt, "Commentaire", PdfText.Truncate(parcel.Comment, 80));
                    Field(fields, Pt, "Nature du produit", PdfText.Truncate(parcel.Description ?? "—", 60));
                });
                row.ConstantItem(side).Column(right =>
                {
                    right.Spacing(Pt(2.5f));
                    right.Item().Element(Band).PaddingVertical(Pt(2.5f)).PaddingHorizontal(Pt(3)).AlignCenter().Text(parcel.DeliveryModeLabel).FontSize(Pt(7.5f)).Bold().ClampLines(1);
                    if (parcel.OpenOnDelivery) right.Item().Element(Band).PaddingVertical(Pt(2)).PaddingHorizontal(Pt(3)).AlignCenter().Text("يسمح بفتح الطلبية").FontSize(Pt(7)).ClampLines(1);
                    right.Item().Element(Frame).PaddingVertical(Pt(2)).AlignCenter().Column(cod =>
                    {
                        cod.Item().AlignCenter().Text("CRBT").FontSize(Pt(6.5f)).Bold();
                        cod.Item().AlignCenter().Text(PdfText.Amount(parcel.CodToCollect)).FontSize(Pt(10.5f)).Bold().ClampLines(1);
                        var payment = cod.Item().AlignCenter().Text(parcel.ExpectedPaymentLabel ?? parcel.PaymentModeLabel).FontSize(Pt(6)).ClampLines(1);
                        if (parcel.ExpectedPaymentLabel is not null) payment.Bold();
                        if (carton is not null) cod.Item().PaddingTop(Pt(1)).AlignCenter().Text($"Carton {carton}").FontSize(Pt(7)).Bold().ClampLines(1);
                    });
                });
            });
            if (!string.IsNullOrEmpty(brand.Banner))
            {
                column.Item().Element(Band).PaddingVertical(Pt(2)).PaddingHorizontal(Pt(4)).AlignCenter().Text(brand.Banner).FontSize(Pt(7)).ClampLines(1);
            }
            column.Item().AlignCenter().Column(codes =>
            {
                codes.Item().Width(unit - Pt(8)).Height(Pt(22)).Svg(PdfService.Barcode(parcel.Code));
                codes.Item().AlignCenter().Text(carton is null ? parcel.Code : $"{parcel.Code}  ·  {carton}").FontSize(Pt(8)).Bold().LetterSpacing(0.05f);
                if (!string.IsNullOrEmpty(parcel.ExternalCode))
                {
                    codes.Item().PaddingTop(Pt(1)).AlignCenter().Width(unit - Pt(24)).Height(Pt(12)).Svg(PdfService.Barcode(parcel.ExternalCode));
                    codes.Item().AlignCenter().Text($"Réf. {PdfText.Truncate(parcel.ExternalCode, 32)}").FontSize(Pt(6)).LetterSpacing(0.03f);
                }
                codes.Item().AlignCenter().Text($"{PdfText.Truncate(parcel.ClientName, 28)} vous remercie pour votre confiance.").FontSize(Pt(6)).ClampLines(1);
            });
        });
    }

    private static void Cell(IContainer container, Func<float, float> pt, string text, string? background, bool bordered)
    {
        var cell = container.PaddingHorizontal(pt(1));
        if (bordered) cell = cell.Border(0.7f).BorderColor(Colors.Black);
        if (background is not null) cell = cell.Background(background);
        cell.PaddingVertical(pt(2)).PaddingHorizontal(pt(2)).AlignCenter().AlignMiddle().Text(PdfText.Truncate(text, 22)).FontSize(pt(7.5f)).Bold().ClampLines(1);
    }

    private static void Field(ColumnDescriptor column, Func<float, float> pt, string label, string value, bool strong = false, int lines = 1)
    {
        column.Item().Row(row =>
        {
            row.Spacing(pt(2));
            row.AutoItem().Text($"{label} :").FontSize(pt(6.5f)).Bold();
            var text = row.RelativeItem().Text(value).FontSize(pt(7)).ClampLines(lines);
            if (strong) text.Bold();
        });
    }
}
