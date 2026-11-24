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

    /// <summary>`SlipHeader` : logo assombri (36 pt de haut), raison sociale, code, date d'impression et QR.</summary>
    private static void SlipHeader(ColumnDescriptor column, PdfCompany company, byte[]? logo, string subtitle, string code, DateTime printedAt)
    {
        column.Item().Row(row =>
        {
            row.RelativeItem().Column(head =>
            {
                if (LabelLogo.ForLightBackground(logo) is { } image) head.Item().PaddingBottom(6).Height(36).MaxWidth(160).AlignLeft().Image(image).FitArea();
                head.Item().Text(PdfText.Truncate(company.Title, 34)).FontSize(15).Bold();
                head.Item().PaddingTop(2).Text(subtitle).FontColor(Muted);
                foreach (var line in company.HeaderLines()) head.Item().Text(PdfText.Truncate(line, 52)).FontColor(Muted);
            });
            row.AutoItem().Column(right =>
            {
                right.Item().AlignRight().Text(code).FontSize(14).Bold().LetterSpacing(0.04f);
                right.Item().AlignRight().Text($"Le {Exports.DateTimeText(printedAt)}").FontColor(Muted);
                right.Item().AlignRight().PaddingTop(4).Width(62).Height(62).Image(PdfService.Qr(code));
            });
        });
        BusinessDocuments.Rule(column);
    }

    /// <summary>`SignatureBox` : cadre de 240 pt, nom sur la première ligne, filets gris pour la date et la signature.</summary>
    private static void SignatureBox(IContainer container, string title, string name, IReadOnlyList<string> lines) =>
        container.Width(240).MinHeight(150).Border(0.8f).BorderColor(Ink).Padding(10).Column(box =>
        {
            BusinessDocuments.Caption(box.Item(), title);
            for (var i = 0; i < lines.Count; i++)
            {
                var last = i == lines.Count - 1;
                box.Item().PaddingTop(last ? 14 : 10).Column(entry =>
                {
                    entry.Item().Text(text =>
                    {
                        text.Span($"{lines[i]} : ").FontColor(Muted);
                        if (i == 0) text.Span(name).FontColor(Ink);
                    });
                    if (i > 0) entry.Item().PaddingTop(last ? 34 : 12).LineHorizontal(0.4f).LineColor("#999999");
                });
            }
        });

    private static void SlipSignatures(ColumnDescriptor column, float top, string courierName, string clientName, string courierLast)
    {
        column.Item().PaddingTop(top).Row(row =>
        {
            SignatureBox(row.AutoItem(), "Remis par (livreur)", courierName, ["Nom", "Date", courierLast]);
            row.RelativeItem();
            SignatureBox(row.AutoItem(), "Reçu par le vendeur", clientName, ["Nom", "Date", "Signature et cachet"]);
        });
    }

    public static byte[] ChequeSlip(ChequeSlipSheet s)
    {
        const string footer = "Ce bon atteste la remise du chèque au vendeur ; il ne change pas son statut.";
        (string Label, string Value, bool Strong)[] rows =
        [
            ("N° du chèque", s.Number, true), ("Banque", s.Bank ?? "—", false), ("Montant", PdfText.Amount(s.Amount), true), ("Colis", s.ParcelCode, true),
            ("Vendeur", s.ClientName, false), ("Livreur", s.CourierName, false), ("Destinataire (tireur)", s.RecipientName, false),
        ];
        return Document.Create(container => BusinessDocuments.Page(container, s.Company, footer, column =>
        {
            SlipHeader(column, s.Company, s.Logo, "Bon de remise de chèque", s.Code, s.PrintedAt);
            column.Item().PaddingTop(8).Column(list =>
            {
                foreach (var (label, value, strong) in rows)
                {
                    list.Item().ShowEntire().BorderBottom(0.4f).BorderColor(BusinessDocuments.RowRule).PaddingVertical(5).PaddingHorizontal(6).Row(row =>
                    {
                        BusinessDocuments.Caption(row.ConstantItem(150), label);
                        var text = row.RelativeItem().Text(PdfText.Truncate(value, 60)).FontSize(11);
                        if (strong) text.Bold();
                    });
                }
            });
            SlipSignatures(column, 40, s.CourierName, s.ClientName, "Signature");
        })).WithMetadata(new DocumentMetadata { Title = s.Code }).GeneratePdf();
    }

    public static byte[] ChequeGroupSlip(ChequeGroupSlipSheet sheet)
    {
        const string footer = "Ce bon atteste la remise des chèques au vendeur ; il ne change pas leur statut.";
        return Document.Create(container =>
        {
            foreach (var slip in sheet.Slips)
            {
                var total = slip.Cheques.Sum(c => c.Amount);
                var count = slip.Cheques.Count;
                BusinessDocuments.Page(container, sheet.Company, footer, column =>
                {
                    SlipHeader(column, sheet.Company, sheet.Logo, "Bon de remise de chèques", slip.Code, sheet.PrintedAt);
                    column.Item().Row(row =>
                    {
                        BusinessDocuments.Caption(row.ConstantItem(70), "Vendeur");
                        row.RelativeItem().Text(PdfText.Truncate(slip.ClientName, 60)).Bold();
                    });
                    column.Item().PaddingTop(12).Background("#F1F1F1").PaddingVertical(5).PaddingHorizontal(6).Row(head =>
                    {
                        BusinessDocuments.Caption(head.ConstantItem(80), "N° chèque");
                        BusinessDocuments.Caption(head.ConstantItem(70), "Banque");
                        BusinessDocuments.Caption(head.ConstantItem(78).AlignRight(), "Montant");
                        BusinessDocuments.Caption(head.ConstantItem(100).PaddingLeft(8), "Colis");
                        BusinessDocuments.Caption(head.ConstantItem(90), "Livreur");
                        BusinessDocuments.Caption(head.RelativeItem(), "Tireur");
                    });
                    foreach (var cheque in slip.Cheques)
                    {
                        ReturnDocumentsPdf.Line(column, row =>
                        {
                            row.ConstantItem(80).Text(PdfText.Truncate(cheque.Number, 14)).Bold();
                            row.ConstantItem(70).Text(PdfText.Truncate(cheque.Bank ?? "—", 12));
                            row.ConstantItem(78).AlignRight().Text(PdfText.Amount(cheque.Amount));
                            row.ConstantItem(100).PaddingLeft(8).Text(cheque.ParcelCode);
                            row.ConstantItem(90).Text(PdfText.Truncate(cheque.CourierName, 16));
                            row.RelativeItem().Text(PdfText.Truncate(cheque.PayerName, 22));
                        });
                    }
                    column.Item().ShowEntire().Column(end =>
                    {
                        end.Item().PaddingTop(10).Row(row =>
                        {
                            row.AutoItem().Text($"{count} chèque{(count > 1 ? "s" : string.Empty)}").Bold();
                            row.RelativeItem();
                            row.AutoItem().Text($"Total : {PdfText.Amount(total)}").FontSize(11).Bold();
                        });
                        SlipSignatures(end, 36, slip.CourierName ?? string.Empty, slip.ClientName, "Signature");
                    });
                });
            }
        }).WithMetadata(new DocumentMetadata { Title = string.Join(" · ", sheet.Slips.Select(s => s.Code)) }).GeneratePdf();
    }

    private static string NoonDate(string day) => Exports.DateText(DateTime.SpecifyKind(DateTime.ParseExact($"{day}T12:00:00", "yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc));

    /// <summary>Une rangée `styles.between` de plusieurs textes : écarts égaux entre eux.</summary>
    private static void Spread(IContainer container, IReadOnlyList<(string Text, bool Strong)> items) => container.Row(row =>
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0) row.RelativeItem();
            var text = row.AutoItem().Text(items[i].Text);
            if (items[i].Strong) text.Bold();
        }
    });

    public static byte[] PettyCash(PettyCashSheet s)
    {
        const string footer = "Petite caisse — dépenses courantes, hors facturation.";
        return Document.Create(container => BusinessDocuments.Page(container, s.Company, footer, column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(head =>
                {
                    if (s.Logo is { } logo) head.Item().PaddingBottom(4).Height(36).MaxWidth(160).AlignLeft().Image(logo).FitArea();
                    head.Item().Text(PdfText.Truncate(s.Company.Title, 34)).FontSize(15).Bold();
                    foreach (var line in s.Company.HeaderLines()) head.Item().Text(PdfText.Truncate(line, 52)).FontColor(Muted);
                });
                row.AutoItem().Column(right =>
                {
                    right.Item().AlignRight().Text("PETITE CAISSE").FontSize(14).Bold().LetterSpacing(0.04f);
                    right.Item().AlignRight().Text($"Du {NoonDate(s.From)} au {NoonDate(s.To)}").FontColor(Muted);
                    right.Item().AlignRight().Text($"Généré le {Exports.DateText(s.GeneratedAt)}").FontColor(Muted);
                });
            });
            BusinessDocuments.Rule(column);
            if (s.Day is { } day)
            {
                Spread(column.Item().PaddingVertical(6), [($"Journée {(day.Status == "closed" ? "clôturée" : "ouverte")}", true), ($"Ouverture : {PdfText.Amount(day.OpeningBalance)}", false),
                    ($"Approvisionné : {PdfText.Amount(day.Supplied)}", false), ($"Dépensé : {PdfText.Amount(day.Spent)}", false), ($"Clôture : {PdfText.Amount(day.ClosingBalance)}", true)]);
                var notes = new List<string>();
                if (day.Status == "closed" && day.ClosedAt is { } closedAt) notes.Add($"Clôturée le {Exports.DateText(closedAt)} — {(day.ClosedBy == "automatic" ? "automatique" : day.ClosedBy ?? string.Empty)}");
                if (day.PendingDrafts > 0) notes.Add($"{day.PendingDrafts} dépense(s) de livreur à valider, hors solde");
                if (!string.IsNullOrEmpty(day.ReopenReason)) notes.Add($"Dernière correction : {PdfText.Truncate(day.ReopenReason, 60)}");
                column.Item().Text(string.Join(" · ", notes)).FontColor(Muted);
            }
            else
            {
                Spread(column.Item().PaddingVertical(6), [($"Solde actuel : {PdfText.Amount(s.Balance)}", true), ($"Approvisionné : {PdfText.Amount(s.Supplied)}", false),
                    ($"Dépensé : {PdfText.Amount(s.Spent)}", false)]);
            }
            column.Item().PaddingTop(6).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(58 + 6);
                    columns.RelativeColumn();
                    columns.ConstantColumn(78);
                    columns.ConstantColumn(92);
                    columns.ConstantColumn(72 + 6);
                });
                table.Header(header =>
                {
                    static IContainer Head(IContainer cell) => cell.Background("#F1F1F1").PaddingVertical(5);
                    BusinessDocuments.Caption(Head(header.Cell()).PaddingLeft(6), "Date");
                    BusinessDocuments.Caption(Head(header.Cell()), "Libellé");
                    BusinessDocuments.Caption(Head(header.Cell()), "Catégorie");
                    BusinessDocuments.Caption(Head(header.Cell()), "Acheté par");
                    BusinessDocuments.Caption(Head(header.Cell()).PaddingRight(6).AlignRight(), "Montant");
                });
                foreach (var line in s.Rows)
                {
                    static IContainer Cell(IContainer cell) => cell.BorderBottom(0.4f).BorderColor(BusinessDocuments.RowRule).PaddingVertical(5);
                    Cell(table.Cell()).PaddingLeft(6).Text(NoonDate(line.Date));
                    Cell(table.Cell()).Text(PdfText.Truncate(line.Kind == "supply" ? $"Approvisionnement{(string.IsNullOrEmpty(line.Note) ? string.Empty : $" — {line.Note}")}" : line.Label ?? string.Empty, 48));
                    Cell(table.Cell()).Text(PdfText.Truncate(line.Category ?? string.Empty, 16));
                    Cell(table.Cell()).Text(PdfText.Truncate(line.BuyerName ?? string.Empty, 20));
                    Cell(table.Cell()).PaddingRight(6).AlignRight().Text($"{(line.Kind == "expense" ? "−" : "+")}{PdfText.Amount(line.Amount)}").Bold();
                }
            });
            if (s.Rows.Count == 0) column.Item().PaddingTop(10).Text("Aucun mouvement sur cette période.").FontColor(Muted);
        })).WithMetadata(new DocumentMetadata { Title = "Petite caisse" }).GeneratePdf();
    }

    private static readonly Dictionary<string, string> StatementStates = new() { ["draft"] = "Brouillon, non validé", ["validated"] = "Validé", ["paid"] = "Payé" };
    private static readonly Dictionary<string, string> PayModels = new() { ["salary"] = "Fixe", ["commission"] = "À la course", ["hybrid"] = "Fixe + courses" };
    private static readonly Dictionary<string, string> CourseKinds = new() { ["pickup"] = "Ramassage", ["delivery"] = "Livraison", ["return"] = "Retour" };

    public static byte[] CourierStatement(CourierStatementSheet s)
    {
        const string footer = "Montants figés à la validation du relevé. Une course n’est payée qu’une seule fois.";
        var variableCounts = s.PayModel != "salary";
        return Document.Create(container => BusinessDocuments.Page(container, s.Company, footer, column =>
        {
            ReturnDocumentsPdf.Header(column, s.Company, "Relevé de rémunération", s.Code, s.AgencyName, null);
            ReturnDocumentsPdf.Blocks(column,
            [
                (240, c => ReturnDocumentsPdf.Block(c, "Livreur", s.CourierName, $"Mode : {PayModels.GetValueOrDefault(s.PayModel, s.PayModel)}",
                    $"Commissions : {(s.SettlementMode == "at-remittance" ? "déduites à chaque remise de fonds" : "cumulées sur ce relevé")}")),
                (240, c => ReturnDocumentsPdf.Block(c, "Période", $"du {Exports.CalendarDateText(s.PeriodStart)} au {Exports.CalendarDateText(s.PeriodEnd)}",
                    StatementStates.GetValueOrDefault(s.Status, s.Status))),
            ]);
            ReturnDocumentsPdf.Head(column, head =>
            {
                BusinessDocuments.Caption(head.ConstantItem(62), "Date");
                BusinessDocuments.Caption(head.ConstantItem(118), "Colis");
                BusinessDocuments.Caption(head.ConstantItem(74), "Course");
                BusinessDocuments.Caption(head.RelativeItem(), "Ville");
                BusinessDocuments.Caption(head.ConstantItem(60), "Réglée");
                BusinessDocuments.Caption(head.ConstantItem(74).AlignRight(), "Montant");
            });
            foreach (var line in s.Lines)
            {
                ReturnDocumentsPdf.Line(column, row =>
                {
                    row.ConstantItem(62).Text(Exports.DateText(line.WorkedAt));
                    row.ConstantItem(118).Text(line.ParcelCode).Bold();
                    row.ConstantItem(74).Text(CourseKinds.GetValueOrDefault(line.Kind, line.Kind));
                    row.RelativeItem().Text(PdfText.Truncate(line.CityName, 24));
                    row.ConstantItem(60).Text(line.Deducted ? "à la remise" : "-");
                    row.ConstantItem(74).AlignRight().Text(PdfText.Amount(line.Amount));
                });
            }
            if (s.Lines.Count == 0) column.Item().PaddingTop(10).Text("Aucune course confirmée sur la période.").FontColor(Muted);
            column.Item().PaddingTop(14).Row(row =>
            {
                row.AutoItem().Text($"{s.LineCount} courses").Bold();
                row.RelativeItem();
                row.ConstantItem(240).Column(totals =>
                {
                    if (s.PayModel != "commission") Between(totals, 0, "Fixe de la période", PdfText.Amount(s.BaseAmount));
                    Between(totals, 3, "COD encaissé (pour mémoire)", PdfText.Amount(s.CodCollected));
                    Between(totals, 3, $"Commissions{(variableCounts ? string.Empty : " (pour information)")}", PdfText.Amount(s.CommissionsTotal));
                    if (s.CommissionsDeducted > 0) Between(totals, 3, "Dont déjà gardées aux remises", $"- {PdfText.Amount(s.CommissionsDeducted)}");
                    Between(totals, 3, "Reste dû sur les courses", PdfText.Amount(s.VariableAmount));
                    totals.Item().PaddingVertical(6).LineHorizontal(0.6f).LineColor(Ink);
                    Between(totals, 0, "Net à payer", PdfText.Amount(s.TotalDue), strong: true, valueSize: 12);
                });
            });
            column.Item().PaddingTop(36).Row(row =>
            {
                row.ConstantItem(210).Column(c =>
                {
                    BusinessDocuments.Caption(c.Item(), "Le livreur");
                    c.Item().PaddingTop(30).LineHorizontal(0.6f).LineColor(Ink);
                });
                row.RelativeItem();
                Signed(row.ConstantItem(210), "L’agence", s.ValidatedByName);
            });
        })).WithMetadata(new DocumentMetadata { Title = s.Code }).GeneratePdf();
    }

    private static readonly Dictionary<string, string> PayoutStates = new() { ["draft"] = "Brouillon, non versé", ["paid"] = "Versé" };
    private static readonly Dictionary<string, string> Methods = new() { ["cash"] = "Espèces", ["transfer"] = "Virement", ["check"] = "Chèque" };

    private static string ChequeState(string status, PdfCompany company) => status switch
    {
        "recu-livreur" => "Reçu par le livreur",
        "remis-admin" => $"Remis à {company.ShortText}",
        "recu-admin" => $"Reçu par {company.ShortText}",
        "transmis-vendeur" => "Transmis au vendeur",
        "recu-vendeur" => "Réception confirmée par le vendeur",
        "rejete" => "Rejeté",
        "annule" => "Annulé",
        _ => status,
    };

    public static byte[] CodPayout(CodPayoutSheet p)
    {
        var footer = string.IsNullOrWhiteSpace(p.Footer) ? "Montants figés au versement. Le COD d’un colis n’est versé qu’une seule fois." : p.Footer;
        return Document.Create(container => BusinessDocuments.Page(container, p.Company, footer, column =>
        {
            ReturnDocumentsPdf.Header(column, p.Company, "Bon de versement", p.Code, p.AgencyName, null);
            var method = Methods.GetValueOrDefault(p.Method, p.Method) + (p.BankAccountLabel is null ? string.Empty : $" — {PdfText.Truncate(p.BankAccountLabel, 20)}");
            var when = (p.PaidAt is { } paid ? Exports.DateText(paid) : "Non versé") + (p.Reference is null ? string.Empty : $" · réf. {PdfText.Truncate(p.Reference, 24)}");
            ReturnDocumentsPdf.Blocks(column,
            [
                (240, c => ReturnDocumentsPdf.Block(c, "E-commerçant", p.ClientName, PayoutStates.GetValueOrDefault(p.Status, p.Status))),
                (240, c => ReturnDocumentsPdf.Block(c, "Versement", method, when)),
            ]);
            ReturnDocumentsPdf.Head(column, head =>
            {
                BusinessDocuments.Caption(head.ConstantItem(130), "Colis");
                BusinessDocuments.Caption(head.RelativeItem(), "Destinataire");
                BusinessDocuments.Caption(head.ConstantItem(110), "Ville");
                BusinessDocuments.Caption(head.ConstantItem(80).AlignRight(), "Montant");
            });
            foreach (var item in p.Items)
            {
                ReturnDocumentsPdf.Line(column, row =>
                {
                    row.ConstantItem(130).Text(item.ParcelCode).Bold();
                    row.RelativeItem().Text(PdfText.Truncate(item.RecipientName, 24));
                    row.ConstantItem(110).Text(PdfText.Truncate(item.DestinationCityName, 18));
                    row.ConstantItem(80).AlignRight().Text(PdfText.Amount(item.Amount));
                });
            }
            if (p.Items.Count == 0) column.Item().PaddingTop(10).Text("Aucun colis sur ce bon.").FontColor(Muted);
            column.Item().PaddingTop(14).Row(row =>
            {
                row.AutoItem().Text($"{p.ParcelCount} colis").Bold();
                row.RelativeItem();
                row.ConstantItem(250).Column(totals =>
                {
                    Between(totals, 0, "Total versé", PdfText.Amount(p.Amount), strong: true, valueSize: 12);
                    totals.Item().PaddingTop(4).Text("COD encaissé en espèces, sans déduction de frais. Les chèques sont transmis séparément, non versés. Les frais de livraison font l’objet d’une facture distincte.")
                        .FontColor(Muted);
                });
            });
            if (p.Cheques.Count > 0)
            {
                column.Item().PaddingTop(16).Column(block =>
                {
                    block.Item().ShowEntire().Column(head =>
                    {
                        head.Item().Text($"Chèques reçus ({p.Cheques.Count})").Bold();
                        head.Item().PaddingTop(2).Text(p.Cheques.Any(c => c.IncludedInPayout)
                            ? "Pour information. Bon versé sous une règle antérieure (avant le 29/10/2026, ou chèque rejeté avant le 05/11/2026) : la part chèque des colis marqués * a été versée dans ce bon."
                            : "Pour information. Les chèques sont remis séparément, ils ne sont pas inclus dans le montant versé.").FontColor(Muted);
                        head.Item().PaddingTop(6).Background("#F1F1F1").PaddingVertical(5).PaddingHorizontal(6).Row(cells =>
                        {
                            BusinessDocuments.Caption(cells.ConstantItem(96), "N° chèque");
                            BusinessDocuments.Caption(cells.ConstantItem(80), "Banque");
                            BusinessDocuments.Caption(cells.ConstantItem(120), "Colis");
                            BusinessDocuments.Caption(cells.RelativeItem(), "État");
                            BusinessDocuments.Caption(cells.ConstantItem(80).AlignRight(), "Montant");
                        });
                    });
                    foreach (var cheque in p.Cheques)
                    {
                        ReturnDocumentsPdf.Line(block, cells =>
                        {
                            cells.ConstantItem(96).Text($"{PdfText.Truncate(cheque.Number, 14)}{(cheque.IncludedInPayout ? " *" : string.Empty)}").Bold();
                            cells.ConstantItem(80).Text(PdfText.Truncate(cheque.Bank ?? "—", 12));
                            cells.ConstantItem(120).Text(cheque.ParcelCode);
                            cells.RelativeItem().Text(ChequeState(cheque.Status, p.Company));
                            cells.ConstantItem(80).AlignRight().Text(PdfText.Amount(cheque.Amount));
                        });
                    }
                    Between(block, 6, "Total des chèques", PdfText.Amount(p.Cheques.Where(c => c.Status != "annule").Sum(c => c.Amount)), strong: true);
                    foreach (var rejected in p.Cheques.Where(c => c.Status == "rejete"))
                    {
                        block.Item().PaddingTop(3).Text($"Chèque n° {PdfText.Truncate(rejected.Number, 20)} rejeté : rendu au vendeur, non payé par {p.Company.ShortText}").FontColor(Muted);
                    }
                });
            }
            if (!string.IsNullOrEmpty(p.Note))
            {
                column.Item().PaddingTop(12).Column(note =>
                {
                    BusinessDocuments.Caption(note.Item(), "Note");
                    note.Item().PaddingTop(2).Text(PdfText.Truncate(p.Note, 200));
                });
            }
            ReturnDocumentsPdf.Signatures(column, c => Signed(c, "L’agence", p.PaidByName), c => Signed(c, "L’e-commerçant", p.ClientName));
        })).WithMetadata(new DocumentMetadata { Title = p.Code }).GeneratePdf();
    }

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
