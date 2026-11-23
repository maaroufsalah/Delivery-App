using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class CourierCashSession
{
    public string Id { get; set; } = Cuid.New();
    public string CourierId { get; set; } = string.Empty;
    public string AgencyId { get; set; } = string.Empty;
    public DateOnly Day { get; set; }
    public CashSessionStatus Status { get; set; } = CashSessionStatus.OPEN;
    public decimal TotalCash { get; set; } = 0m;
    public decimal TotalCheque { get; set; } = 0m;
    public decimal TotalCollected { get; set; } = 0m;
    public decimal TotalCommission { get; set; } = 0m;
    public decimal ExpectedHandover { get; set; } = 0m;
    public decimal? DeclaredAmount { get; set; }
    public decimal Variance { get; set; } = 0m;
    public SettlementMode SettlementMode { get; set; } = SettlementMode.AT_REMITTANCE;
    public PayModel PayModel { get; set; } = PayModel.COMMISSION;
    public string? Note { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public string? ValidatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Courier Courier { get; set; } = null!;
    public Agency Agency { get; set; } = null!;
    public User? ValidatedBy { get; set; }
    public ICollection<CourierCashSessionParcel> Parcels { get; set; } = [];
    public ICollection<CourierBalanceEntry> Entries { get; set; } = [];
    public ICollection<Cheque> Cheques { get; set; } = [];
}
