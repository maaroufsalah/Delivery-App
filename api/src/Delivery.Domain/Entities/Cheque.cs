using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class Cheque
{
    public string Id { get; set; } = Cuid.New();
    public ChequeStatus Status { get; set; } = ChequeStatus.RECU_LIVREUR;
    public string Number { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Bank { get; set; }
    public DateOnly? ChequeDate { get; set; }
    public DateTime ReceivedAt { get; set; }
    public string PayerName { get; set; } = string.Empty;
    public string ParcelId { get; set; } = string.Empty;
    public string CourierId { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public string? RemittanceId { get; set; }
    public string? InvoiceId { get; set; }
    public DateTime? HandedOverAt { get; set; }
    public DateTime? ReceivedByAdminAt { get; set; }
    public DateTime? TransmittedAt { get; set; }
    public DateTime? MerchantReceivedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? DepositedAt { get; set; }
    public DateTime? CashedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Parcel Parcel { get; set; } = null!;
    public Courier Courier { get; set; } = null!;
    public CourierCashSession? Session { get; set; }
    public CashRemittance? Remittance { get; set; }
    public Invoice? Invoice { get; set; }
    public ICollection<ChequeEvent> Events { get; set; } = [];
}
