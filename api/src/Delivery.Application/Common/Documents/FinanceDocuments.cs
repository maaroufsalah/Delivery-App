namespace Delivery.Application.Common.Documents;

/// <summary>`ChequeSlipDocument` : le bon de remise d'un chèque au vendeur.</summary>
public sealed record ChequeSlipSheet(PdfCompany Company, byte[]? Logo, string Code, DateTime PrintedAt, string Number, string? Bank, long Amount, string ParcelCode, string ClientName,
    string CourierName, string RecipientName);

public sealed record ChequeGroupSlipLine(string Number, string? Bank, long Amount, string ParcelCode, string CourierName, string PayerName);

public sealed record ChequeGroupSlip(string Code, string ClientName, string? CourierName, IReadOnlyList<ChequeGroupSlipLine> Cheques);

/// <summary>`ChequeGroupSlipDocument` : un bon par vendeur, une page chacun, dans un seul PDF.</summary>
public sealed record ChequeGroupSlipSheet(PdfCompany Company, byte[]? Logo, DateTime PrintedAt, IReadOnlyList<ChequeGroupSlip> Slips);

public sealed record PettyCashSheetLine(string Date, string Kind, string? Label, string? Note, string? Category, string? BuyerName, long Amount);

public sealed record PettyCashSheetDay(string Status, long OpeningBalance, long Supplied, long Spent, long ClosingBalance, DateTime? ClosedAt, string? ClosedBy, int PendingDrafts,
    string? ReopenReason);

/// <summary>`PettyCashDocument` : le relevé de petite caisse (ou d'une journée), logo de la société en tête.</summary>
public sealed record PettyCashSheet(PdfCompany Company, byte[]? Logo, DateTime GeneratedAt, string From, string To, long Balance, long Supplied, long Spent, PettyCashSheetDay? Day,
    IReadOnlyList<PettyCashSheetLine> Rows);

public sealed record CourierStatementSheetLine(DateTime WorkedAt, string ParcelCode, string Kind, string CityName, bool Deducted, long Amount);

/// <summary>`CourierStatementDocument` : le relevé de rémunération d'un livreur.</summary>
public sealed record CourierStatementSheet(PdfCompany Company, string Code, string Status, string AgencyName, string CourierName, string PayModel, string SettlementMode, DateOnly PeriodStart,
    DateOnly PeriodEnd, int LineCount, long BaseAmount, long CodCollected, long CommissionsTotal, long CommissionsDeducted, long VariableAmount, long TotalDue, string? ValidatedByName,
    IReadOnlyList<CourierStatementSheetLine> Lines);

public sealed record CodPayoutSheetLine(string ParcelCode, string RecipientName, string DestinationCityName, long Amount);

public sealed record CodPayoutSheetCheque(string Number, string? Bank, string ParcelCode, string Status, long Amount, bool IncludedInPayout);

/// <summary>`CodPayoutDocument` : le bon de versement du COD à un e-commerçant professionnel.</summary>
public sealed record CodPayoutSheet(PdfCompany Company, string? Footer, string Code, string Status, string AgencyName, string ClientName, string Method, string? BankAccountLabel, DateTime? PaidAt,
    string? Reference, int ParcelCount, long Amount, string? Note, string? PaidByName, IReadOnlyList<CodPayoutSheetLine> Items, IReadOnlyList<CodPayoutSheetCheque> Cheques);

public sealed record InvoiceSheetLine(string ParcelCode, string? ExternalCode, string? RecipientName, string? RecipientPhone, string? DestinationCityName, DateTime? DeliveredAt, int StatusId,
    long CodAmount, long Fees, long NetDue, bool ChequeRejected, string? ChequeNumber);

public sealed record InvoiceSheetAdjustment(string Reason, string? Reference, long Amount);

public sealed record InvoiceSheetPayment(DateTime PaidAt, string? BankAccountLabel, string? Reference, long Amount);

/// <summary>Une facture (une page et ses suites) : `InvoicePage`.</summary>
public sealed record InvoiceSheetPage(string Code, string Status, string ClientName, bool ClientIsPro, string AgencyName, string BillingType, DateTime? PeriodStart, DateTime? PeriodEnd,
    DateTime? IssuedAt, DateTime? DueAt, int ParcelCount, long TotalCod, long TotalDeliveryFee, long TotalFailureFee, long TotalAdjustment, long TotalVat, int VatRate, int? ProVatRate,
    long TotalDue, long TotalPaid, long Balance, string? Note, IReadOnlyList<InvoiceSheetLine> Lines, IReadOnlyList<InvoiceSheetAdjustment> Adjustments,
    IReadOnlyList<InvoiceSheetPayment> Payments, string? CarriedIntoCode = null)
{
    /// <summary>`invoiceStateLabel` : l'état imprimé, avec la facture qui absorbe le report quand elle existe.</summary>
    public string StateLabel => Status == "carried-over"
        ? CarriedIntoCode is { } into ? $"Reportée sur {into}" : "Reportée sur la prochaine facture"
        : InvoiceStateLabels.GetValueOrDefault(Status, Status);

    public static readonly IReadOnlyDictionary<string, string> InvoiceStateLabels = new Dictionary<string, string>
    {
        ["draft"] = "Brouillon, non émise", ["issued"] = "Émise", ["paid"] = "Payée", ["carried-over"] = "Reportée",
    };
}

/// <summary>`InvoiceDocument` / `InvoicesDayDocument` : une ou plusieurs factures dans un PDF.</summary>
public sealed record InvoiceSheet(PdfCompany Company, string? ProLegalName, string? ProTaxId, byte[]? Logo, string? Footer, string Title, IReadOnlyList<InvoiceSheetPage> Invoices);

public sealed record TransferOrderSheetLine(string ClientName, int ParcelCount, long Collected, long Fees, long Due);

/// <summary>`TransferOrderDocument` : l'ordre de virement (OV-) aux vendeurs.</summary>
public sealed record TransferOrderSheet(PdfCompany Company, byte[]? Logo, string Code, string Source, DateTime CreatedAt, string? CreatedByName, IReadOnlyList<string> InvoiceCodes,
    long TotalCollected, long TotalFees, long TotalDue, IReadOnlyList<TransferOrderSheetLine> Lines);

public sealed record RemittanceSheetLine(string ParcelCode, string ClientName, string RecipientName, long Amount, long Commission);

public sealed record RemittanceSheetCheque(string Number, string? Bank, string ParcelCode, long Amount);

/// <summary>`RemittanceReceiptDocument` : le reçu de remise de fonds (RF-).</summary>
public sealed record RemittanceSheet(PdfCompany Company, string Code, string Status, string AgencyName, string CourierName, string CityName, DateTime CreatedAt, string SettlementMode,
    int ParcelCount, long ExpectedCod, long TotalCheque, int ChequeCount, long TotalCash, long ExpectedCommission, long ExpectedNet, long? DeclaredAmount, long Variance, string? VarianceReason,
    string? ConfirmedByName, IReadOnlyList<RemittanceSheetLine> Items, IReadOnlyList<RemittanceSheetCheque> Cheques);
