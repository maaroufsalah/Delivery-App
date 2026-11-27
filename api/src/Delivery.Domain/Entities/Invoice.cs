using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class Invoice
{
    public string Id { get; set; } = Cuid.New();
    public string Code { get; set; } = string.Empty;
    public InvoiceStatus Status { get; set; } = InvoiceStatus.DRAFT;
    public string ClientId { get; set; } = string.Empty;
    public string AgencyId { get; set; } = string.Empty;
    public string? Note { get; set; }
    public decimal TotalCod { get; set; } = 0m;
    public decimal TotalDeliveryFee { get; set; } = 0m;
    public decimal TotalFailureFee { get; set; } = 0m;
    public decimal TotalVat { get; set; } = 0m;
    public decimal TotalAdjustment { get; set; } = 0m;
    public decimal TotalDue { get; set; } = 0m;
    public int VatRate { get; set; } = 0;
    public int? ProVatRate { get; set; }
    public BillingType BillingType { get; set; } = BillingType.NORMAL;
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    public DateTime? IssuedAt { get; set; }
    public string? IssuedById { get; set; }
    public DateTime? DueAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? PaidById { get; set; }
    public string? CarriedIntoId { get; set; }
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Client Client { get; set; } = null!;
    public Agency Agency { get; set; } = null!;
    public User? IssuedBy { get; set; }
    public User? PaidBy { get; set; }
    public Invoice? CarriedInto { get; set; }
    public InvoiceAdjustment? CarryOverLine { get; set; }
    public ICollection<InvoiceLine> Lines { get; set; } = [];
    public ICollection<InvoiceAdjustment> Adjustments { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
    public ICollection<Cheque> Cheques { get; set; } = [];
}
