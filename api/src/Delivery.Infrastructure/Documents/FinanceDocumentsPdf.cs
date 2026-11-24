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

    private static readonly Dictionary<string, string> InvoiceStates = new() { ["draft"] = "Brouillon, non émise", ["issued"] = "Émise", ["paid"] = "Payée" };

    /// <summary>`breakLong` : un mot plus long que la colonne est coupé en morceaux, un par ligne.</summary>
    private static string BreakLong(string value, int max) => string.Concat(System.Text.RegularExpressions.Regex.Split(value.Trim(), @"(\s+)").Select(part =>
    {
        if (part.Length == 0 || char.IsWhiteSpace(part[0]) || part.Length <= max) return part;
        var pieces = new List<string>();
        for (var i = 0; i < part.Length; i += max) pieces.Add(part.Substring(i, Math.Min(max, part.Length - i)));
        return string.Join('\n', pieces);
    }));

    private static string LineStatus(int statusId) => statusId switch { 4 => "Livré", 5 => "Retourné", 6 => "Refusé", _ => $"Statut {statusId}" };

    private static void InvoicePage(IDocumentContainer container, InvoiceSheet sheet, InvoiceSheetPage i)
    {
        var company = i.ClientIsPro
            ? sheet.Company with { LegalName = string.IsNullOrWhiteSpace(sheet.ProLegalName) ? sheet.Company.LegalName : sheet.ProLegalName.Trim(),
                TaxId = string.IsNullOrWhiteSpace(sheet.ProTaxId) ? sheet.Company.TaxId : sheet.ProTaxId.Trim() }
            : sheet.Company;
        var logo = i.ClientIsPro ? sheet.Logo : null;
        var feesHt = i.TotalDeliveryFee + i.TotalFailureFee + i.TotalAdjustment;
        var feesTtc = feesHt + i.TotalVat;
        var professional = i.BillingType == "professional";
        var cheques = professional ? -i.TotalDue - feesTtc : i.TotalCod - feesTtc - i.TotalDue;
        var toSettle = professional ? Math.Abs(i.TotalDue) : i.TotalDue;
        var period = i.PeriodStart is { } ps && i.PeriodEnd is { } pe
            ? $"{Exports.CalendarDateText(DateOnly.FromDateTime(ps))} — {Exports.CalendarDateText(DateOnly.FromDateTime(pe))}"
            : null;
        var footer = string.IsNullOrWhiteSpace(sheet.Footer) ? "Montants figés à l’émission de la facture. Un colis n’est facturé qu’une seule fois." : sheet.Footer;
        BusinessDocuments.Page(container, company, footer, column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(head =>
                {
                    if (logo is not null) head.Item().PaddingBottom(6).Height(36).MaxWidth(160).AlignLeft().Image(logo).FitArea();
                    head.Item().Text(PdfText.Truncate(company.Title, 34)).FontSize(15).Bold();
                    head.Item().PaddingTop(2).Text(period is null ? "Facture" : "Facture mensuelle").FontColor(Muted);
                    foreach (var line in company.HeaderLines()) head.Item().Text(PdfText.Truncate(line, 52)).FontColor(Muted);
                    if (!string.IsNullOrWhiteSpace(company.TaxId)) head.Item().Text($"ICE : {company.TaxId}").FontColor(Muted);
                });
                row.AutoItem().Column(right =>
                {
                    right.Item().AlignRight().Text(i.Code).FontSize(14).Bold().LetterSpacing(0.04f);
                    right.Item().AlignRight().Text($"Agence : {PdfText.Truncate(i.AgencyName, 24)}").FontColor(Muted);
                    right.Item().AlignRight().PaddingTop(4).Width(62).Height(62).Image(PdfService.Qr(i.Code));
                });
            });
            BusinessDocuments.Rule(column);
            ReturnDocumentsPdf.Blocks(column,
            [
                (240, c => ReturnDocumentsPdf.Block(c, "E-commerçant", i.ClientName, null)),
                (240, c => c.Column(state =>
                {
                    BusinessDocuments.Caption(state.Item(), "État");
                    state.Item().PaddingTop(2).Text(InvoiceStates.GetValueOrDefault(i.Status, i.Status)).FontSize(11);
                    if (period is not null) state.Item().Text($"Période : {period}").FontColor(Muted);
                    if (i.IssuedAt is { } issued) state.Item().Text($"Émise le {Exports.DateText(issued)}").FontColor(Muted);
                    if (i.DueAt is { } due) state.Item().Text($"Échéance : {Exports.CalendarDateText(DateOnly.FromDateTime(due))}").FontColor(Muted);
                })),
            ]);
            column.Item().PaddingTop(16).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(104 + 6);
                    columns.ConstantColumn(107);
                    columns.ConstantColumn(54);
                    columns.ConstantColumn(46);
                    columns.ConstantColumn(68);
                    columns.ConstantColumn(68);
                    columns.ConstantColumn(68 + 6);
                });
                table.Header(header =>
                {
                    static IContainer Head(IContainer cell) => cell.Background("#F1F1F1").PaddingVertical(5);
                    BusinessDocuments.Caption(Head(header.Cell()).PaddingLeft(6), "Colis");
                    BusinessDocuments.Caption(Head(header.Cell()), "Destinataire");
                    BusinessDocuments.Caption(Head(header.Cell()), "Date");
                    BusinessDocuments.Caption(Head(header.Cell()), "Statut");
                    BusinessDocuments.Caption(Head(header.Cell()).AlignRight(), "Encaissé");
                    BusinessDocuments.Caption(Head(header.Cell()).AlignRight(), "Frais HT");
                    BusinessDocuments.Caption(Head(header.Cell()).PaddingRight(6).AlignRight(), "Net");
                });
                foreach (var line in i.Lines)
                {
                    static IContainer Cell(IContainer cell) => cell.BorderBottom(0.4f).BorderColor(BusinessDocuments.RowRule).PaddingVertical(5);
                    Cell(table.Cell()).PaddingLeft(6).PaddingRight(6).Column(c =>
                    {
                        c.Item().Text(BreakLong(line.ParcelCode, 20)).FontSize(8).Bold();
                        if (!string.IsNullOrEmpty(line.ExternalCode)) c.Item().Text($"Réf. {BreakLong(line.ExternalCode, 20)}").FontSize(7).FontColor(Muted);
                    });
                    var contact = string.Join(" · ", new[] { line.RecipientPhone, line.DestinationCityName }.Select(v => v?.Trim()).Where(v => !string.IsNullOrEmpty(v)));
                    Cell(table.Cell()).PaddingRight(6).Column(c =>
                    {
                        if (!string.IsNullOrEmpty(line.RecipientName)) c.Item().Text(BreakLong(line.RecipientName, 19));
                        if (contact.Length > 0) c.Item().Text(BreakLong(contact, 25)).FontSize(7).FontColor(Muted);
                    });
                    Cell(table.Cell()).Text(line.DeliveredAt is { } at ? Exports.DateText(at) : "—");
                    Cell(table.Cell()).Text(LineStatus(line.StatusId));
                    Cell(table.Cell()).AlignRight().Text(PdfText.Amount(line.CodAmount));
                    Cell(table.Cell()).AlignRight().Text(PdfText.Amount(line.Fees));
                    Cell(table.Cell()).PaddingRight(6).AlignRight().Text(PdfText.Amount(line.NetDue));
                }
            });
            if (i.Adjustments.Count > 0)
            {
                column.Item().PaddingTop(12).ShowEntire().Column(block =>
                {
                    block.Item().Background("#F1F1F1").PaddingVertical(5).PaddingHorizontal(6).Row(head =>
                    {
                        BusinessDocuments.Caption(head.RelativeItem(), "Ajustements");
                        BusinessDocuments.Caption(head.ConstantItem(96), "Référence");
                        BusinessDocuments.Caption(head.ConstantItem(74).AlignRight(), "Frais HT");
                    });
                    foreach (var line in i.Adjustments)
                    {
                        ReturnDocumentsPdf.Line(block, row =>
                        {
                            row.RelativeItem().Text(PdfText.Truncate(line.Reason, 90));
                            row.ConstantItem(96).Text(PdfText.Truncate(line.Reference ?? string.Empty, 16));
                            row.ConstantItem(74).AlignRight().Text(PdfText.Amount(line.Amount));
                        });
                    }
                });
            }
            var rejected = i.Lines.Where(l => l.ChequeRejected).ToList();
            if (rejected.Count > 0)
            {
                column.Item().PaddingTop(10).ShowEntire().Column(block =>
                {
                    foreach (var line in rejected)
                    {
                        block.Item().Text($"Chèque n° {PdfText.Truncate(line.ChequeNumber ?? "—", 20)} ({line.ParcelCode}) rejeté : rendu au vendeur, non payé par {sheet.Company.ShortText}").FontColor(Muted);
                    }
                });
            }
            column.Item().PaddingTop(14).ShowEntire().Row(row =>
            {
                row.AutoItem().Text($"{i.ParcelCount} colis").Bold();
                row.RelativeItem();
                row.ConstantItem(240).Column(totals =>
                {
                    Between(totals, 0, professional ? "Encaissé, versé à part" : "Encaissé pour votre compte", PdfText.Amount(i.TotalCod));
                    Between(totals, 3, i.ProVatRate is not null ? "Total HT" : "Frais HT", PdfText.Amount(feesHt));
                    if (i.TotalAdjustment != 0)
                    {
                        totals.Item().PaddingTop(1).Row(r =>
                        {
                            r.AutoItem().Text("dont ajustements").FontColor(Muted);
                            r.RelativeItem();
                            r.AutoItem().Text(PdfText.Amount(i.TotalAdjustment)).FontColor(Muted);
                        });
                    }
                    if (i.ProVatRate is { } proRate)
                    {
                        Between(totals, 3, "TVA", Delivery.Application.Invoices.InvoiceSheets.VatRate(proRate));
                        Between(totals, 3, "Montant TVA", PdfText.Amount(i.TotalVat));
                        Between(totals, 3, "Total TTC", PdfText.Amount(feesTtc));
                    }
                    else if (i.VatRate > 0)
                    {
                        Between(totals, 3, $"TVA {(i.VatRate / 100m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',')} %", PdfText.Amount(i.TotalVat));
                        Between(totals, 3, "Frais TTC", PdfText.Amount(feesTtc));
                    }
                    if (cheques > 0) Between(totals, 3, "Chèques reçus directement", PdfText.Amount(cheques));
                    totals.Item().PaddingVertical(6).LineHorizontal(0.6f).LineColor(Ink);
                    Between(totals, 0, professional ? "Total à régler" : "Net à reverser", PdfText.Amount(toSettle), strong: true, valueSize: 12);
                    if (professional)
                    {
                        totals.Item().PaddingTop(4).Text("La part espèces vous a été versée par bon de versement ; les chèques vous sont transmis directement.").FontColor(Muted);
                    }
                    if (i.TotalPaid > 0)
                    {
                        Between(totals, 3, "Déjà réglé", PdfText.Amount(i.TotalPaid));
                        Between(totals, 3, "Reste à régler", PdfText.Amount(i.Balance), strong: true);
                    }
                });
            });
            if (i.Payments.Count > 0)
            {
                column.Item().PaddingTop(20).Column(list =>
                {
                    BusinessDocuments.Caption(list.Item(), "Règlements");
                    foreach (var payment in i.Payments)
                    {
                        list.Item().PaddingTop(4).Row(row =>
                        {
                            row.AutoItem().Text($"{Exports.DateText(payment.PaidAt)}, {payment.BankAccountLabel ?? "Espèces"}{(payment.Reference is null ? string.Empty : $" ({PdfText.Truncate(payment.Reference, 24)})")}");
                            row.RelativeItem();
                            row.AutoItem().Text(PdfText.Amount(payment.Amount));
                        });
                    }
                });
            }
            if (!string.IsNullOrEmpty(i.Note))
            {
                column.Item().PaddingTop(18).Column(note =>
                {
                    BusinessDocuments.Caption(note.Item(), "Note");
                    note.Item().PaddingTop(2).Text(i.Note);
                });
            }
        });
    }

    public static byte[] Invoice(InvoiceSheet sheet) => Document.Create(container =>
    {
        foreach (var page in sheet.Invoices) InvoicePage(container, sheet, page);
    }).WithMetadata(new DocumentMetadata { Title = sheet.Title }).GeneratePdf();

    public static byte[] TransferOrder(TransferOrderSheet o)
    {
        const string footer = "Frais de livraison facturés TVA comprise. Montant à verser = COD encaissé − frais.";
        return Document.Create(container => BusinessDocuments.Page(container, o.Company, footer, column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(head =>
                {
                    if (o.Logo is not null) head.Item().PaddingBottom(4).Height(36).MaxWidth(160).AlignLeft().Image(o.Logo).FitArea();
                    head.Item().Text(PdfText.Truncate(o.Company.Title, 34)).FontSize(15).Bold();
                    foreach (var line in o.Company.HeaderLines()) head.Item().Text(PdfText.Truncate(line, 52)).FontColor(Muted);
                });
                row.AutoItem().Column(right =>
                {
                    right.Item().AlignRight().Text($"ORDRE DE VIREMENT N° {o.Code}").FontSize(14).Bold().LetterSpacing(0.04f);
                    right.Item().AlignRight().Text($"Généré le {Exports.DateText(o.CreatedAt)}").FontColor(Muted);
                });
            });
            BusinessDocuments.Rule(column);
            column.Item().PaddingTop(8).Background("#F1F1F1").PaddingVertical(5).PaddingHorizontal(6).Row(head =>
            {
                BusinessDocuments.Caption(head.RelativeItem(), "Client");
                BusinessDocuments.Caption(head.ConstantItem(105).AlignRight(), "Montant global (COD encaissé)");
                BusinessDocuments.Caption(head.ConstantItem(105).AlignRight(), "Frais de livraison");
                BusinessDocuments.Caption(head.ConstantItem(105).AlignRight(), "Montant à verser");
            });
            foreach (var line in o.Lines)
            {
                ReturnDocumentsPdf.Line(column, row =>
                {
                    row.RelativeItem().Text(PdfText.Truncate(line.ClientName, 40));
                    row.ConstantItem(105).AlignRight().Text(PdfText.Amount(line.Collected));
                    row.ConstantItem(105).AlignRight().Text(PdfText.Amount(line.Fees));
                    row.ConstantItem(105).AlignRight().Text(PdfText.Amount(line.Due)).Bold();
                });
            }
            if (o.Lines.Count == 0) column.Item().PaddingTop(10).Text("Aucun client sur cet ordre.").FontColor(Muted);
            column.Item().ShowEntire().BorderTop(0.8f).BorderColor(Ink).BorderBottom(0.4f).BorderColor(BusinessDocuments.RowRule).PaddingVertical(5).PaddingHorizontal(6).Row(row =>
            {
                row.RelativeItem().Text("Total").Bold();
                row.ConstantItem(105).AlignRight().Text(PdfText.Amount(o.TotalCollected)).Bold();
                row.ConstantItem(105).AlignRight().Text(PdfText.Amount(o.TotalFees)).Bold();
                row.ConstantItem(105).AlignRight().Text(PdfText.Amount(o.TotalDue)).FontSize(11).Bold();
            });
        })).WithMetadata(new DocumentMetadata { Title = o.Code }).GeneratePdf();
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
