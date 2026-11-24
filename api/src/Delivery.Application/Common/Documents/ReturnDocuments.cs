namespace Delivery.Application.Common.Documents;

public sealed record BlNoteSheetLine(string ParcelCode, string RecipientName, string? Reference);

/// <summary>`BlNoteDocument` : le bon de retour BL (liasse de bons de livraison signés).</summary>
public sealed record BlNoteSheet(PdfCompany Company, string Code, string Status, string AgencyName, DateTime CreatedAt, string ClientName, string CourierName, DateTime? ReceivedAt,
    DateTime? HandedOverAt, string? ReturnedToName, int SlipCount, IReadOnlyList<BlNoteSheetLine> Items);

public sealed record ReturnHandoverSheetLine(string ParcelCode, string RecipientName, string ClientName, int Quantity, int StatusId);

/// <summary>`ReturnHandoverDocument` : le bon de retour colis (livreur → agence).</summary>
public sealed record ReturnHandoverSheet(PdfCompany Company, string Code, string Status, string Origin, string AgencyName, DateTime CreatedAt, string CourierName, string? CreatedByName,
    IReadOnlyList<string> ClientNames, DateTime? ConfirmedAt, DateTime? PickedUpAt, string? ConfirmedByName, int ParcelCount, IReadOnlyList<ReturnHandoverSheetLine> Items);

public sealed record ReturnSlipSheetLine(string ParcelCode, string RecipientName, string Reason, int Quantity, long FeeApplied);

/// <summary>`ReturnSlipDocument` : le bon de retour remis à l'e-commerçant.</summary>
public sealed record ReturnSlipSheet(PdfCompany Company, string Code, string Status, string AgencyName, string ClientName, DateTime? DeliveredAt, string? ReceivedByName, int ParcelCount,
    long TotalFees, IReadOnlyList<ReturnSlipSheetLine> Items);
