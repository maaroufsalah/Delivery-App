using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class InvoiceAdjustment
{
    public string Id { get; set; } = Cuid.New();
    public string InvoiceId { get; set; } = string.Empty;
    public InvoiceAdjustmentKind Kind { get; set; }
    public decimal Amount { get; set; }
    public decimal VatAmount { get; set; } = 0m;
    public string Reason { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string? DeliveryNoteId { get; set; }
    public string? CreatedById { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public DeliveryNote? DeliveryNote { get; set; }
}
