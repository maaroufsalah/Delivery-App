namespace Delivery.Application.Common.Documents;

/// <summary>`ChequeSlipDocument` : le bon de remise d'un chèque au vendeur.</summary>
public sealed record ChequeSlipSheet(PdfCompany Company, byte[]? Logo, string Code, DateTime PrintedAt, string Number, string? Bank, long Amount, string ParcelCode, string ClientName,
    string CourierName, string RecipientName);

public sealed record ChequeGroupSlipLine(string Number, string? Bank, long Amount, string ParcelCode, string CourierName, string PayerName);

public sealed record ChequeGroupSlip(string Code, string ClientName, string? CourierName, IReadOnlyList<ChequeGroupSlipLine> Cheques);

/// <summary>`ChequeGroupSlipDocument` : un bon par vendeur, une page chacun, dans un seul PDF.</summary>
public sealed record ChequeGroupSlipSheet(PdfCompany Company, byte[]? Logo, DateTime PrintedAt, IReadOnlyList<ChequeGroupSlip> Slips);

public sealed record RemittanceSheetLine(string ParcelCode, string ClientName, string RecipientName, long Amount, long Commission);

public sealed record RemittanceSheetCheque(string Number, string? Bank, string ParcelCode, long Amount);

/// <summary>`RemittanceReceiptDocument` : le reçu de remise de fonds (RF-).</summary>
public sealed record RemittanceSheet(PdfCompany Company, string Code, string Status, string AgencyName, string CourierName, string CityName, DateTime CreatedAt, string SettlementMode,
    int ParcelCount, long ExpectedCod, long TotalCheque, int ChequeCount, long TotalCash, long ExpectedCommission, long ExpectedNet, long? DeclaredAmount, long Variance, string? VarianceReason,
    string? ConfirmedByName, IReadOnlyList<RemittanceSheetLine> Items, IReadOnlyList<RemittanceSheetCheque> Cheques);
