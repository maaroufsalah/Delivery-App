using System.Globalization;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Delivery.Infrastructure.Documents;

/// <summary>`route-sheet.tsx` et `delivery-note.tsx` : mêmes blocs, mêmes largeurs de colonnes, même pied.</summary>
internal static class RouteDocumentsPdf
{
    private const string Ink = "#111111";
    private const string Muted = "#6B6B6B";

    private static readonly Dictionary<string, string> SheetTitles = new()
    {
        ["delivery"] = "Feuille de route",
        ["pickup"] = "Feuille de ramassage",
        ["return"] = "Feuille de retour",
    };

    /// <summary>
    /// `textDirection` : chez Speedy, le texte arabe garde sa police mais React-PDF le pose à gauche dans ces tableaux
    /// (largeur ajustée au contenu) : il n'est donc pas aligné à droite ici non plus.
    /// </summary>
    private static TextBlockDescriptor Directed(IContainer container, string text) => container.Text(text);

    private static void Header(ColumnDescriptor column, PdfCompany company, string subtitle, string code)
    {
        column.Item().Row(row =>
        {
            row.RelativeItem().Column(head =>
            {
                head.Item().Text(PdfText.Truncate(company.Title, 34)).FontSize(15).Bold();
                head.Item().PaddingTop(2).Text(subtitle).FontColor(Muted);
                foreach (var line in company.HeaderLines()) head.Item().Text(PdfText.Truncate(line, 52)).FontColor(Muted);
            });
            row.AutoItem().Column(right =>
            {
                right.Item().AlignRight().Text(code).FontSize(14).Bold().LetterSpacing(0.04f);
                right.Item().AlignRight().PaddingTop(4).Width(62).Height(62).Image(PdfService.Qr(code));
            });
        });
        BusinessDocuments.Rule(column);
    }

    /// <summary>`styles.between` de champs « légende / valeur » : premier à gauche, dernier à droite, écarts égaux.</summary>
    private static void Fields(ColumnDescriptor column, IReadOnlyList<(string Label, string Value)> fields, float? maxWidth, int? truncate)
    {
        column.Item().Row(row =>
        {
            for (var i = 0; i < fields.Count; i++)
            {
                if (i > 0) row.RelativeItem();
                var item = maxWidth is { } max ? row.AutoItem().MaxWidth(max) : row.AutoItem();
                var (label, value) = fields[i];
                item.Column(c =>
                {
                    BusinessDocuments.Caption(c.Item(), label);
                    Directed(c.Item().PaddingTop(2), truncate is { } t ? PdfText.Truncate(value, t) : value);
                });
            }
        });
    }

    public static byte[] RouteSheet(RouteSheet route)
    {
        var toSender = route.Kind is "pickup" or "return";
        var title = SheetTitles.GetValueOrDefault(route.Kind, SheetTitles["delivery"]);
        var total = route.Parcels.Sum(p => p.CodToCollect);
        var quantities = route.Parcels.Select(p => p.Quantity).ToList();
        var cartons = Shipments.ShowsCartons(quantities);
        var footer = toSender
            ? "Un colis absent ou refusé au ramassage se déclare dans l’application, pas sur cette feuille."
            : "Les montants sont ceux calculés au départ du circuit. Un colis reporté ou refusé se déclare dans l’application, pas sur cette feuille.";
        return Document.Create(container => BusinessDocuments.Page(container, route.Company, footer, column =>
        {
            Header(column, route.Company, title, route.Code);
            Fields(column, [("Livreur", route.CourierName), ("Ville", route.CityName), ("Sortie", route.ExitDate is null ? "-" : Exports.DateText(route.ExitDate)),
                ("Expéditions", route.Parcels.Count.ToString(CultureInfo.InvariantCulture))], null, null);
            column.Item().PaddingTop(14).Background("#F1F1F1").PaddingVertical(5).PaddingHorizontal(6).Row(head =>
            {
                head.ConstantItem(16);
                BusinessDocuments.Caption(head.ConstantItem(96), Shipments.ColumnFr);
                BusinessDocuments.Caption(head.RelativeItem(), toSender ? "Expéditeur et adresse d’enlèvement" : "Destinataire et adresse");
                if (cartons) BusinessDocuments.Caption(head.ConstantItem(44).AlignRight(), "Cartons");
                if (!toSender) BusinessDocuments.Caption(head.ConstantItem(66).AlignRight(), "À encaisser");
            });
            foreach (var parcel in route.Parcels)
            {
                var name = toSender ? parcel.SenderName : parcel.RecipientName;
                var phone = toSender ? parcel.SenderPhone : parcel.RecipientPhone;
                var place = toSender
                    ? string.Join(", ", new[] { parcel.PickupAddress, parcel.OriginCityName }.Where(s => !string.IsNullOrEmpty(s)))
                    : string.Join(", ", new[] { parcel.DestinationDistrictName, parcel.DeliveryAddress }.Where(s => !string.IsNullOrEmpty(s)));
                column.Item().ShowEntire().BorderBottom(0.4f).BorderColor(BusinessDocuments.RowRule).PaddingVertical(5).PaddingHorizontal(6).Row(row =>
                {
                    // Case à cocher : le livreur pointe au stylo.
                    row.ConstantItem(10).Height(10).Border(0.6f).BorderColor(Ink);
                    row.ConstantItem(6);
                    row.ConstantItem(96).Text(parcel.Code).Bold();
                    row.RelativeItem().Column(person =>
                    {
                        Directed(person.Item(), $"{PdfText.Truncate(name, 34)} · {phone}");
                        Directed(person.Item(), PdfText.Truncate(place, 78)).FontColor(Muted);
                    });
                    if (cartons) row.ConstantItem(44).AlignRight().Text(Shipments.CartonsOf(parcel.Quantity).ToString(CultureInfo.InvariantCulture));
                    if (!toSender) row.ConstantItem(66).AlignRight().Text(PdfText.Amount(parcel.CodToCollect));
                });
            }
            column.Item().PaddingTop(12).Row(row =>
            {
                row.AutoItem().Column(left =>
                {
                    left.Item().Text(Shipments.TotalFr(quantities)).Bold();
                    if (route.DoneCount is { } done)
                    {
                        var removed = route.RemovedCount ?? 0;
                        left.Item().Text($"{done} / {route.Parcels.Count + removed} {(toSender ? "ramassés" : "livrés")} — {Math.Max(route.Parcels.Count - done, 0)} restants"
                                         + (removed > 0 ? $" — {removed} non remis, retirés" : string.Empty)).FontColor(Muted);
                    }
                });
                row.RelativeItem();
                if (!toSender) row.AutoItem().Text($"Total à encaisser : {PdfText.Amount(total)}").Bold();
            });
            column.Item().PaddingTop(36).Row(row =>
            {
                Signature(row.ConstantItem(200), "Livreur", 26);
                row.RelativeItem();
                Signature(row.ConstantItem(200), "Agence", 26);
            });
        })).WithMetadata(new DocumentMetadata { Title = route.Code }).GeneratePdf();
    }

    public static byte[] DeliveryNote(DeliveryNoteSheet note)
    {
        var pickup = note.Kind == "pickup";
        var quantities = note.Parcels.Select(p => p.Quantity).ToList();
        var cartons = Shipments.ShowsCartons(quantities);
        const string footer = "Les frais indiqués sont ceux calculés à la création des colis. La validation du bon les fige ; elle ne se rejoue pas.";
        return Document.Create(container => BusinessDocuments.Page(container, note.Company, footer, column =>
        {
            Header(column, note.Company, pickup ? "Bon de ramassage" : "Bon de dépôt", note.Code);
            Fields(column, [("E-commerçant", note.ClientName), ("Agence", note.AgencyName), (pickup ? "Lieu de ramassage" : "Lieu de dépôt", note.CityName),
                ("Créé le", Exports.DateText(note.CreatedAt))], 130, 24);
            if (note.PickupWaived)
            {
                column.Item().PaddingTop(12).Border(0.6f).BorderColor(Ink).CornerRadius(3).Padding(8).Text("Ramassage offert, au-delà du seuil de colis fixé par la société.");
            }
            column.Item().PaddingTop(14).Background("#F1F1F1").PaddingVertical(5).PaddingHorizontal(6).Row(head =>
            {
                BusinessDocuments.Caption(head.ConstantItem(100), Shipments.ColumnFr);
                BusinessDocuments.Caption(head.RelativeItem(), "Destinataire");
                BusinessDocuments.Caption(head.ConstantItem(120), "Destination");
                if (cartons) BusinessDocuments.Caption(head.ConstantItem(44).AlignRight(), "Cartons");
                BusinessDocuments.Caption(head.ConstantItem(62).AlignRight(), "Contre-rbt");
                BusinessDocuments.Caption(head.ConstantItem(58).AlignRight(), "Frais");
            });
            foreach (var parcel in note.Parcels)
            {
                column.Item().ShowEntire().BorderBottom(0.4f).BorderColor(BusinessDocuments.RowRule).PaddingVertical(5).PaddingHorizontal(6).Row(row =>
                {
                    row.ConstantItem(100).Text(parcel.Code).Bold();
                    row.RelativeItem().Column(who =>
                    {
                        Directed(who.Item(), PdfText.Truncate(parcel.RecipientName, 28));
                        if (!string.IsNullOrEmpty(parcel.Correction)) who.Item().Text(PdfText.Truncate(parcel.Correction, 70)).FontColor(Muted);
                    });
                    row.ConstantItem(120).Text(PdfText.Truncate(string.Join(" · ", new[] { parcel.DestinationDistrictName, parcel.DestinationCityName }.Where(s => !string.IsNullOrEmpty(s))), 24));
                    if (cartons) row.ConstantItem(44).AlignRight().Text(Shipments.CartonsOf(parcel.Quantity).ToString(CultureInfo.InvariantCulture));
                    row.ConstantItem(62).AlignRight().Text(PdfText.Amount(parcel.CodAmount));
                    row.ConstantItem(58).AlignRight().Text(PdfText.Amount(parcel.DeliveryFee));
                });
            }
            if (note.Parcels.Count == 0) column.Item().PaddingTop(10).Text("Aucun colis sur ce bon.").FontColor(Muted);
            column.Item().PaddingTop(12).Row(row =>
            {
                row.AutoItem().Column(left =>
                {
                    left.Item().Text(Shipments.TotalFr(quantities)).Bold();
                    if (pickup && note.PickedCount is { } picked)
                    {
                        var removed = note.RemovedCount ?? 0;
                        left.Item().Text($"{picked} / {note.Parcels.Count + removed} ramassés" + (removed > 0 ? $" — {removed} non remis, retirés du bon" : string.Empty)).FontColor(Muted);
                    }
                });
                row.RelativeItem();
                row.AutoItem().Column(right =>
                {
                    right.Item().AlignRight().Text($"Total contre-remboursement : {PdfText.Amount(note.Parcels.Sum(p => p.CodAmount))}").Bold();
                    right.Item().AlignRight().Text($"Total frais de livraison : {PdfText.Amount(note.Parcels.Sum(p => p.DeliveryFee))}").FontColor(Muted);
                });
            });
            column.Item().PaddingTop(34).Row(row =>
            {
                Signature(row.ConstantItem(210), pickup ? "Remis par l’e-commerçant" : "Déposé par l’e-commerçant", 28);
                row.RelativeItem();
                Signature(row.ConstantItem(210), pickup ? "Ramassé par" : "Reçu par", 28);
            });
            if (note.ValidatedAt is { } validated)
            {
                column.Item().PaddingTop(12).Text($"Bon validé le {Exports.DateText(validated)} : les colis sont passés en « Colis ramassé ».").FontColor(Muted);
            }
        })).WithMetadata(new DocumentMetadata { Title = note.Code }).GeneratePdf();
    }

    private static void Signature(IContainer container, string label, float gap) => container.Column(c =>
    {
        BusinessDocuments.Caption(c.Item(), label);
        c.Item().PaddingTop(gap).LineHorizontal(0.6f).LineColor(Ink);
    });
}
