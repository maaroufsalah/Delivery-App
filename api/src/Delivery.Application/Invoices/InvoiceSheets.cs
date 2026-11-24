using System.Globalization;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;

namespace Delivery.Application.Invoices;

/// <summary>`invoice-export.ts` : la facture en deux feuilles, les colis puis le récapitulatif.</summary>
public static class InvoiceSheets
{
    public static readonly Dictionary<string, string> StatusLabels = new() { ["draft"] = "Brouillon, non émise", ["issued"] = "Émise", ["paid"] = "Payée" };

    private static readonly string[] ParcelHeaders = ["Expédition", "Référence", "Date de livraison", "Nom complet", "Téléphone", "Ville", "Prix", "Frais HT", "Net", "Statut"];

    /// <summary>`formatVatRate` : « 20 % », « 0 % (exonéré) ».</summary>
    public static string VatRate(int rate)
    {
        var percent = (rate / 100m).ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');
        return rate == 0 ? $"{percent} % (exonéré)" : $"{percent} %";
    }

    private sealed record Row(string Label, string? Text = null, long? Amount = null, bool Strong = false, bool Heading = false);

    private static List<Row> Summary(InvoiceDetailDto invoice)
    {
        var feesHt = invoice.TotalDeliveryFee + invoice.TotalFailureFee + invoice.TotalAdjustment;
        var feesTtc = feesHt + invoice.TotalVat;
        var professional = invoice.BillingType == "professional";
        var cheques = professional ? -invoice.TotalDue - feesTtc : invoice.TotalCod - feesTtc - invoice.TotalDue;
        var toSettle = professional ? Math.Abs(invoice.TotalDue) : invoice.TotalDue;
        var period = invoice.PeriodStart is { } ps && invoice.PeriodEnd is { } pe
            ? $"{Exports.CalendarDateText(DateOnly.FromDateTime(ps))} — {Exports.CalendarDateText(DateOnly.FromDateTime(pe))}"
            : null;
        var rows = new List<Row>
        {
            new("Numéro", invoice.Code), new("Document", period is null ? "Facture" : "Facture mensuelle"), new("E-commerçant", invoice.ClientName), new("Agence", invoice.AgencyName),
            new("État", StatusLabels[invoice.Status]),
        };
        if (period is not null) rows.Add(new Row("Période", period));
        if (invoice.IssuedAt is { } issued) rows.Add(new Row("Émise le", Exports.DateText(issued)));
        if (invoice.DueAt is { } due) rows.Add(new Row("Échéance", Exports.CalendarDateText(DateOnly.FromDateTime(due))));
        if (invoice.Adjustments.Count > 0)
        {
            rows.Add(new Row(string.Empty));
            rows.Add(new Row("Ajustements", Heading: true));
            foreach (var line in invoice.Adjustments) rows.Add(new Row(line.Reason, line.Reference ?? string.Empty, line.Amount));
        }
        rows.Add(new Row(string.Empty));
        rows.Add(new Row("Totaux", Heading: true));
        rows.Add(new Row("Colis", invoice.ParcelCount.ToString(CultureInfo.InvariantCulture)));
        rows.Add(new Row(professional ? "Encaissé, versé à part" : "Encaissé pour votre compte", Amount: invoice.TotalCod));
        rows.Add(new Row(invoice.ProVatRate is not null ? "Total HT" : "Frais HT", Amount: feesHt));
        if (invoice.TotalAdjustment != 0) rows.Add(new Row("dont ajustements", Amount: invoice.TotalAdjustment));
        if (invoice.ProVatRate is { } proRate)
        {
            rows.Add(new Row("TVA", VatRate(proRate)));
            rows.Add(new Row("Montant TVA", Amount: invoice.TotalVat));
            rows.Add(new Row("Total TTC", Amount: feesTtc));
        }
        else if (invoice.VatRate > 0)
        {
            rows.Add(new Row($"TVA {(invoice.VatRate / 100m).ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',')} %", Amount: invoice.TotalVat));
            rows.Add(new Row("Frais TTC", Amount: feesTtc));
        }
        if (cheques > 0) rows.Add(new Row("Chèques reçus directement", Amount: cheques));
        rows.Add(new Row(professional ? "Total à régler" : "Net à reverser", Amount: toSettle, Strong: true));
        if (invoice.TotalPaid > 0)
        {
            rows.Add(new Row("Déjà réglé", Amount: invoice.TotalPaid));
            rows.Add(new Row("Reste à régler", Amount: invoice.Balance, Strong: true));
        }
        if (invoice.Payments.Count > 0)
        {
            rows.Add(new Row(string.Empty));
            rows.Add(new Row("Règlements", Heading: true));
            foreach (var payment in invoice.Payments) rows.Add(new Row($"{Exports.DateText(payment.PaidAt)}, {payment.BankAccountLabel ?? "Espèces"}", payment.Reference ?? string.Empty, payment.Amount));
        }
        if (!string.IsNullOrEmpty(invoice.Note))
        {
            rows.Add(new Row(string.Empty));
            rows.Add(new Row("Note", invoice.Note));
        }
        return rows;
    }

    public static IReadOnlyList<ExcelSheet> Build(InvoiceDetailDto invoice, ExcelBanner banner)
    {
        var parcels = invoice.Lines.Select(l => (IReadOnlyList<object?>)
        [
            l.ParcelCode, l.ExternalCode ?? string.Empty, l.DeliveredAt is { } at ? Exports.DateText(at) : string.Empty, l.RecipientName ?? string.Empty, l.RecipientPhone ?? string.Empty,
            l.DestinationCityName ?? string.Empty, Exports.Amount(l.CodAmount), Exports.Amount(l.DeliveryFee + l.FailureFee), Exports.Amount(l.NetDue), DocumentLabels.ParcelStatus(l.StatusId),
        ]).ToList();
        var summary = Summary(invoice);
        return
        [
            new ExcelSheet("Colis", ParcelHeaders, parcels, null, [20, 22, 16, 28, 16, 18, 12, 12, 12, 26], new HashSet<int> { 0, 1, 2, 3, 4, 5, 9 }, new HashSet<int> { 6, 7, 8 }),
            new ExcelSheet("Récapitulatif", null, summary.Select(r => (IReadOnlyList<object?>)[r.Label, r.Text ?? string.Empty, r.Amount is { } a ? Exports.Amount(a) : string.Empty]).ToList(),
                banner, [44, 30, 16], new HashSet<int> { 0, 1 }, new HashSet<int> { 2 },
                summary.Select((r, i) => (r, i)).Where(x => x.r.Strong || x.r.Heading).Select(x => x.i).ToHashSet()),
        ];
    }
}
