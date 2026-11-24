namespace Delivery.Application.Common.Documents;

public sealed record RemittanceSheetLine(string ParcelCode, string ClientName, string RecipientName, long Amount, long Commission);

public sealed record RemittanceSheetCheque(string Number, string? Bank, string ParcelCode, long Amount);

/// <summary>`RemittanceReceiptDocument` : le reçu de remise de fonds (RF-).</summary>
public sealed record RemittanceSheet(PdfCompany Company, string Code, string Status, string AgencyName, string CourierName, string CityName, DateTime CreatedAt, string SettlementMode,
    int ParcelCount, long ExpectedCod, long TotalCheque, int ChequeCount, long TotalCash, long ExpectedCommission, long ExpectedNet, long? DeclaredAmount, long Variance, string? VarianceReason,
    string? ConfirmedByName, IReadOnlyList<RemittanceSheetLine> Items, IReadOnlyList<RemittanceSheetCheque> Cheques);
