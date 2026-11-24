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

public sealed record RemittanceSheetLine(string ParcelCode, string ClientName, string RecipientName, long Amount, long Commission);

public sealed record RemittanceSheetCheque(string Number, string? Bank, string ParcelCode, long Amount);

/// <summary>`RemittanceReceiptDocument` : le reçu de remise de fonds (RF-).</summary>
public sealed record RemittanceSheet(PdfCompany Company, string Code, string Status, string AgencyName, string CourierName, string CityName, DateTime CreatedAt, string SettlementMode,
    int ParcelCount, long ExpectedCod, long TotalCheque, int ChequeCount, long TotalCash, long ExpectedCommission, long ExpectedNet, long? DeclaredAmount, long Variance, string? VarianceReason,
    string? ConfirmedByName, IReadOnlyList<RemittanceSheetLine> Items, IReadOnlyList<RemittanceSheetCheque> Cheques);
