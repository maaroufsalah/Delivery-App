using System.Globalization;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Delivery.Infrastructure.Documents;

/// <summary>`bl-note.tsx`, `return-handover.tsx`, `return-slip.tsx` : même en-tête, mêmes colonnes, mêmes deux signatures.</summary>
internal static class ReturnDocumentsPdf
{
    private const string Ink = "#111111";
    private const string Muted = "#6B6B6B";
    private const string ShortNameToken = "%shortName%";

    private static readonly Dictionary<string, string> BlNoteStates = new()
    {
        ["draft"] = "Brouillon", ["awaiting-receipt"] = "En attente de réception", ["received"] = $"Reçu par {ShortNameToken}", ["returned"] = "Remis au vendeur", ["closed"] = "Clôturé",
    };

    private static readonly Dictionary<string, string> HandoverStates = new()
    {
        ["draft"] = "Brouillon", ["return-requested"] = "Retour demandé", ["picked-up"] = "Pris en charge par le livreur", ["awaiting-receipt"] = "En attente de réception",
        ["confirmed"] = $"Reçu par {ShortNameToken}", ["closed"] = "Clôturé", ["cancelled"] = "Annulé",
    };

    private static readonly Dictionary<string, string> Reasons = new()
    {
        ["refused"] = "Refusé", ["unreachable"] = "Injoignable", ["cancelled"] = "Annulé", ["damaged"] = "Endommagé", ["wrong-address"] = "Adresse erronée", ["other"] = "Autre",
    };

    private static string WithShortName(string text, PdfCompany company) => text.Replace(ShortNameToken, company.ShortText, StringComparison.Ordinal);

    private static void Header(ColumnDescriptor column, PdfCompany company, string subtitle, string code, string agencyName, DateTime? createdAt)
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
                right.Item().AlignRight().Text($"Agence : {PdfText.Truncate(agencyName, 24)}").FontColor(Muted);
                if (createdAt is { } created) right.Item().AlignRight().Text($"Le {Exports.DateText(created)}").FontColor(Muted);
                right.Item().AlignRight().PaddingTop(4).Width(62).Height(62).Image(PdfService.Qr(code));
            });
        });
        BusinessDocuments.Rule(column);
    }

    /// <summary>Un bloc « légende / valeur 11 pt / ligne grise » ; `styles.between` répartit les blocs.</summary>
    private static void Block(IContainer container, string label, string value, string? muted, string? extra = null) => container.Column(c =>
    {
        BusinessDocuments.Caption(c.Item(), label);
        c.Item().PaddingTop(2).Text(value).FontSize(11);
        if (muted is not null) c.Item().Text(muted).FontColor(Muted);
        if (extra is not null) c.Item().Text(extra).FontColor(Muted);
    });

    private static void Blocks(ColumnDescriptor column, IReadOnlyList<(float MaxWidth, Action<IContainer> Draw)> blocks)
    {
        column.Item().Row(row =>
        {
            for (var i = 0; i < blocks.Count; i++)
            {
                if (i > 0) row.RelativeItem();
                blocks[i].Draw(row.AutoItem().MaxWidth(blocks[i].MaxWidth));
            }
        });
    }

    private static void Head(ColumnDescriptor column, Action<RowDescriptor> cells) =>
        column.Item().PaddingTop(16).Background("#F1F1F1").PaddingVertical(5).PaddingHorizontal(6).Row(cells);

    private static void Line(ColumnDescriptor column, Action<RowDescriptor> cells) =>
        column.Item().ShowEntire().BorderBottom(0.4f).BorderColor(BusinessDocuments.RowRule).PaddingVertical(5).PaddingHorizontal(6).Row(cells);

    private static void Totals(ColumnDescriptor column, string left, string right, bool rightStrong)
    {
        column.Item().PaddingTop(12).Row(row =>
        {
            row.AutoItem().Text(left).Bold();
            row.RelativeItem();
            var text = row.AutoItem().Text(right);
            if (rightStrong) text.Bold();
            else text.FontColor(Muted);
        });
    }

    /// <summary>Signature nommée : légende, nom (12 pt minimum), filet 16 pt plus bas.</summary>
    private static void Signed(IContainer container, string label, string? name) => container.Column(c =>
    {
        BusinessDocuments.Caption(c.Item(), label);
        c.Item().PaddingTop(2).MinHeight(12).Text(name ?? string.Empty);
        c.Item().PaddingTop(16).LineHorizontal(0.6f).LineColor(Ink);
    });

    private static void Signatures(ColumnDescriptor column, Action<IContainer> left, Action<IContainer> right)
    {
        column.Item().PaddingTop(36).Row(row =>
        {
            left(row.ConstantItem(210));
            row.RelativeItem();
            right(row.ConstantItem(210));
        });
    }

    public static byte[] BlNote(BlNoteSheet note)
    {
        const string footer = "Ce bon accompagne des bons de livraison signés. Il ne remplace aucun d’entre eux.";
        return Document.Create(container => BusinessDocuments.Page(container, note.Company, footer, column =>
        {
            Header(column, note.Company, "Bon de retour BL", note.Code, note.AgencyName, note.CreatedAt);
            Blocks(column,
            [
                (230, c => Block(c, "Client Pro", note.ClientName, null)),
                (200, c => Block(c, "Livreur", note.CourierName, null)),
                (160, c => Block(c, "État", WithShortName(BlNoteStates[note.Status], note.Company), note.ReceivedAt is { } r ? $"Reçu le {Exports.DateText(r)}" : null)),
            ]);
            Head(column, head =>
            {
                BusinessDocuments.Caption(head.ConstantItem(120), "Colis");
                BusinessDocuments.Caption(head.RelativeItem(), "Destinataire");
                BusinessDocuments.Caption(head.ConstantItem(120), "Référence BL");
            });
            foreach (var item in note.Items)
            {
                Line(column, row =>
                {
                    row.ConstantItem(120).Text(item.ParcelCode).Bold();
                    row.RelativeItem().Text(PdfText.Truncate(item.RecipientName, 30));
                    row.ConstantItem(120).Text(item.Reference ?? "—");
                });
            }
            Totals(column, $"{note.SlipCount} BL", note.HandedOverAt is { } h ? $"Remis à l’entrepôt le {Exports.DateText(h)}" : string.Empty, false);
            Signatures(column, c => Signed(c, "Remis par (livreur)", note.CourierName), c => Signed(c, "Reçu par (vendeur)", note.ReturnedToName));
        })).WithMetadata(new DocumentMetadata { Title = note.Code }).GeneratePdf();
    }

    public static byte[] ReturnHandover(ReturnHandoverSheet handover)
    {
        const string footer = "Les deux signatures n’attestent pas la même chose : le livreur rend la marchandise, l’agence la reçoit.";
        var quantities = handover.Items.Select(i => i.Quantity).ToList();
        var cartons = Shipments.ShowsCartons(quantities);
        var clients = handover.ClientNames.Count > 4
            ? $"{string.Join(", ", handover.ClientNames.Take(4))} +{handover.ClientNames.Count - 4}"
            : handover.ClientNames.Count == 0 ? "—" : string.Join(", ", handover.ClientNames);
        var origin = handover.Origin == "admin"
            ? $"Créé par {handover.Company.ShortText}{(string.IsNullOrEmpty(handover.CreatedByName) ? string.Empty : $" — {handover.CreatedByName}")}"
            : "Créé par le livreur";
        return Document.Create(container => BusinessDocuments.Page(container, handover.Company, footer, column =>
        {
            Header(column, handover.Company, "Bon de retour colis", handover.Code, handover.AgencyName, handover.CreatedAt);
            Blocks(column,
            [
                (200, c => Block(c, "Livreur", handover.CourierName, origin)),
                (200, c => Block(c, "Vendeur(s)", PdfText.Truncate(clients, 40), null)),
                (160, c => Block(c, "État", WithShortName(HandoverStates[handover.Status], handover.Company),
                    handover.ConfirmedAt is { } r ? $"Reçu le {Exports.DateText(r)}" : null)),
            ]);
            Head(column, head =>
            {
                BusinessDocuments.Caption(head.ConstantItem(110), Shipments.ColumnFr);
                BusinessDocuments.Caption(head.RelativeItem(), "Destinataire");
                BusinessDocuments.Caption(head.ConstantItem(130), "Vendeur");
                if (cartons) BusinessDocuments.Caption(head.ConstantItem(44).AlignRight(), "Cartons");
                BusinessDocuments.Caption(head.ConstantItem(40).AlignRight(), "Statut");
            });
            foreach (var item in handover.Items)
            {
                Line(column, row =>
                {
                    row.ConstantItem(110).Text(item.ParcelCode).Bold();
                    row.RelativeItem().Text(PdfText.Truncate(item.RecipientName, 26));
                    row.ConstantItem(130).Text(PdfText.Truncate(item.ClientName, 22));
                    if (cartons) row.ConstantItem(44).AlignRight().Text(Shipments.CartonsOf(item.Quantity).ToString(CultureInfo.InvariantCulture));
                    row.ConstantItem(40).AlignRight().Text(item.StatusId.ToString(CultureInfo.InvariantCulture));
                });
            }
            Totals(column, Shipments.TotalFr(quantities, handover.ParcelCount),
                handover.PickedUpAt is { } p ? $"Pris en charge le {Exports.DateText(p)}" : string.Empty, false);
            Signatures(column, c => Signed(c, "Remis par (livreur)", handover.CourierName), c => Signed(c, "Reçu par (agence)", handover.ConfirmedByName));
        })).WithMetadata(new DocumentMetadata { Title = handover.Code }).GeneratePdf();
    }

    public static byte[] ReturnSlip(ReturnSlipSheet slip)
    {
        const string footer = "Les frais indiqués sont ceux appliqués à la création du bon. Ils ne changent plus.";
        var quantities = slip.Items.Select(i => i.Quantity).ToList();
        var cartons = Shipments.ShowsCartons(quantities);
        return Document.Create(container => BusinessDocuments.Page(container, slip.Company, footer, column =>
        {
            Header(column, slip.Company, "Bon de retour", slip.Code, slip.AgencyName, null);
            Blocks(column,
            [
                (240, c => Block(c, "E-commerçant", slip.ClientName, null)),
                (240, c => Block(c, "État", slip.Status == "delivered" ? "Remis au client" : "Brouillon, non remis", slip.DeliveredAt is { } d ? $"Le {Exports.DateText(d)}" : null)),
            ]);
            Head(column, head =>
            {
                BusinessDocuments.Caption(head.ConstantItem(110), Shipments.ColumnFr);
                BusinessDocuments.Caption(head.RelativeItem(), "Destinataire");
                BusinessDocuments.Caption(head.ConstantItem(110), "Motif");
                if (cartons) BusinessDocuments.Caption(head.ConstantItem(44).AlignRight(), "Cartons");
                BusinessDocuments.Caption(head.ConstantItem(64).AlignRight(), "Frais");
            });
            foreach (var item in slip.Items)
            {
                Line(column, row =>
                {
                    row.ConstantItem(110).Text(item.ParcelCode).Bold();
                    row.RelativeItem().Text(PdfText.Truncate(item.RecipientName, 28));
                    row.ConstantItem(110).Text(Reasons.GetValueOrDefault(item.Reason, item.Reason));
                    if (cartons) row.ConstantItem(44).AlignRight().Text(Shipments.CartonsOf(item.Quantity).ToString(CultureInfo.InvariantCulture));
                    row.ConstantItem(64).AlignRight().Text(PdfText.Amount(item.FeeApplied));
                });
            }
            Totals(column, Shipments.TotalFr(quantities, slip.ParcelCount), $"Total frais : {PdfText.Amount(slip.TotalFees)}", true);
            Signatures(column, c => Signed(c, "Reçu par", slip.ReceivedByName), c =>
                c.Column(s =>
                {
                    BusinessDocuments.Caption(s.Item(), "Signature");
                    s.Item().PaddingTop(30).LineHorizontal(0.6f).LineColor(Ink);
                }));
        })).WithMetadata(new DocumentMetadata { Title = slip.Code }).GeneratePdf();
    }
}
