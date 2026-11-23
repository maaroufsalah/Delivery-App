using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class TransferOrder
{
    public string Id { get; set; } = Cuid.New();
    public string Code { get; set; } = string.Empty;
    public TransferOrderSource Source { get; set; }
    public string? AgencyId { get; set; }
    public string Lines { get; set; } = string.Empty;
    public List<string> InvoiceCodes { get; set; } = [];
    public decimal TotalCollected { get; set; }
    public decimal TotalFees { get; set; }
    public decimal TotalDue { get; set; }
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public Agency? Agency { get; set; }
    public User? CreatedBy { get; set; }
    public ICollection<TransferOrderParcel> Parcels { get; set; } = [];
}
