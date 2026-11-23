using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class CourierBalanceEntry
{
    public string Id { get; set; } = Cuid.New();
    public string CourierId { get; set; } = string.Empty;
    public CourierBalanceEntryKind Kind { get; set; }
    public decimal Amount { get; set; }
    public string? SessionId { get; set; }
    public string? RemittanceId { get; set; }
    public PaymentMethod? Method { get; set; }
    public string? Note { get; set; }
    public string? AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public DateTime At { get; set; }
    public Courier Courier { get; set; } = null!;
    public CourierCashSession? Session { get; set; }
    public CashRemittance? Remittance { get; set; }
    public User? Author { get; set; }
}
