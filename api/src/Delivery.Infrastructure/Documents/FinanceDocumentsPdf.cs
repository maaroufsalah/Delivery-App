using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Delivery.Infrastructure.Documents;

/// <summary>`remittance-receipt.tsx` : même en-tête que les bons, totaux à droite, liste des chèques, deux signatures.</summary>
internal static class FinanceDocumentsPdf
{
    private const string Ink = "#111111";
    private const string Muted = "#6B6B6B";

    private static readonly Dictionary<string, string> RemittanceStates = new()
    {
        ["draft"] = "Ouvert, non confirmé", ["confirmed"] = "Confirmé", ["cancelled"] = "Annulé",
    };

    /// <summary>Une ligne `styles.between` : libellé à gauche, montant à droite.</summary>
    private static void Between(ColumnDescriptor column, float top, string label, string value, bool strong = false, float? valueSize = null)
    {
        column.Item().PaddingTop(top).Row(row =>
        {
            var left = row.AutoItem().Text(label);
            if (strong) left.Bold();
            else left.FontColor(Muted);
            row.RelativeItem();
            var right = row.AutoItem().Text(value);
            if (strong) right.Bold();
            if (valueSize is { } size) right.FontSize(size);
        });
    }

    private static void Signed(IContainer container, string label, string? name) => container.Column(c =>
    {
        BusinessDocuments.Caption(c.Item(), label);
        c.Item().PaddingTop(2).MinHeight(12).Text(name ?? string.Empty).FontColor(Muted);
        c.Item().PaddingTop(16).LineHorizontal(0.6f).LineColor(Ink);
    });

    public static byte[] Remittance(RemittanceSheet r)
    {
        const string footer = "Montants figés à la confirmation. Le cash d’un colis ne remonte qu’une seule fois.";
        var deducts = r.SettlementMode == "at-remittance";
        return Document.Create(container => BusinessDocuments.Page(container, r.Company, footer, column =>
        {
            ReturnDocumentsPdf.Header(column, r.Company, "Reçu de remise de fonds", r.Code, r.AgencyName, null);
            ReturnDocumentsPdf.Blocks(column,
            [
                (240, c => ReturnDocumentsPdf.Block(c, "Livreur", r.CourierName, deducts ? "Commission déduite à la remise" : "Commission reportée sur le relevé")),
                (240, c => ReturnDocumentsPdf.Block(c, "Remise", $"{PdfText.Truncate(r.CityName, 28)} — {Exports.DateText(r.CreatedAt)}", RemittanceStates.GetValueOrDefault(r.Status, r.Status))),
            ]);
            ReturnDocumentsPdf.Head(column, head =>
            {
                BusinessDocuments.Caption(head.ConstantItem(118), "Colis");
                BusinessDocuments.Caption(head.RelativeItem(), "E-commerçant");
                BusinessDocuments.Caption(head.ConstantItem(108), "Destinataire");
                BusinessDocuments.Caption(head.ConstantItem(70).AlignRight(), "Encaissé");
                if (deducts) BusinessDocuments.Caption(head.ConstantItem(66).AlignRight(), "Commission");
            });
            foreach (var item in r.Items)
            {
                ReturnDocumentsPdf.Line(column, row =>
                {
                    row.ConstantItem(118).Text(item.ParcelCode).Bold();
                    row.RelativeItem().Text(PdfText.Truncate(item.ClientName, 22));
                    row.ConstantItem(108).Text(PdfText.Truncate(item.RecipientName, 20));
                    row.ConstantItem(70).AlignRight().Text(PdfText.Amount(item.Amount));
                    if (deducts) row.ConstantItem(66).AlignRight().Text(PdfText.Amount(item.Commission));
                });
            }
            if (r.Items.Count == 0) column.Item().PaddingTop(10).Text("Aucun colis sur cette remise.").FontColor(Muted);
            column.Item().PaddingTop(14).Row(row =>
            {
                row.AutoItem().Text($"{r.ParcelCount} colis").Bold();
                row.RelativeItem();
                row.ConstantItem(250).Column(totals =>
                {
                    Between(totals, 0, "COD encaissé", PdfText.Amount(r.ExpectedCod));
                    if (r.TotalCheque > 0)
                    {
                        Between(totals, 3, $"Chèques ({r.ChequeCount})", $"− {PdfText.Amount(r.TotalCheque)}");
                        Between(totals, 3, "Espèces encaissées", PdfText.Amount(r.TotalCash));
                    }
                    Between(totals, 3, deducts ? "Commissions déduites" : "Commissions (sur le relevé)", deducts ? $"− {PdfText.Amount(r.ExpectedCommission)}" : "—");
                    totals.Item().PaddingVertical(6).LineHorizontal(0.6f).LineColor(Ink);
                    Between(totals, 0, "Net attendu en espèces", PdfText.Amount(r.ExpectedNet), strong: true, valueSize: 12);
                    if (r.DeclaredAmount is { } declared)
                    {
                        Between(totals, 6, "Montant remis", PdfText.Amount(declared));
                        if (r.Variance != 0) Between(totals, 3, "Écart", PdfText.Amount(r.Variance), strong: true);
                    }
                });
            });
            if (r.Cheques.Count > 0)
            {
                column.Item().PaddingTop(14).Column(list =>
                {
                    list.Item().Text($"Chèques remis ({r.Cheques.Count})").Bold();
                    foreach (var cheque in r.Cheques)
                    {
                        list.Item().PaddingTop(4).Row(row =>
                        {
                            row.AutoItem().Text(cheque.Bank is null ? cheque.Number : $"{cheque.Number} — {cheque.Bank}");
                            row.RelativeItem();
                            row.AutoItem().Text(cheque.ParcelCode).FontColor(Muted);
                            row.RelativeItem();
                            row.AutoItem().Text(PdfText.Amount(cheque.Amount));
                        });
                    }
                });
            }
            if (!string.IsNullOrEmpty(r.VarianceReason))
            {
                column.Item().PaddingTop(12).Column(reason =>
                {
                    BusinessDocuments.Caption(reason.Item(), "Motif de l’écart");
                    reason.Item().PaddingTop(2).Text(PdfText.Truncate(r.VarianceReason, 160));
                });
            }
            ReturnDocumentsPdf.Signatures(column, c => Signed(c, "Le livreur", r.CourierName), c => Signed(c, "L’agence", r.ConfirmedByName));
        })).WithMetadata(new DocumentMetadata { Title = r.Code }).GeneratePdf();
    }
}
