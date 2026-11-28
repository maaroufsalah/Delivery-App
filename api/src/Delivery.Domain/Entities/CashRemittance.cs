using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class CashRemittance
{
    public string Id { get; set; } = Cuid.New();
    public string Code { get; set; } = string.Empty;
    public CashRemittanceStatus Status { get; set; } = CashRemittanceStatus.DRAFT;
    public RemittanceOrigin Origin { get; set; } = RemittanceOrigin.MANUAL;
    public string CityId { get; set; } = string.Empty;
    public string CourierId { get; set; } = string.Empty;
    public string AgencyId { get; set; } = string.Empty;
    public decimal Amount { get; set; } = 0m;
    public string? Note { get; set; }
    public decimal ExpectedCod { get; set; } = 0m;
    public decimal ExpectedCommission { get; set; } = 0m;
    public decimal ExpectedNet { get; set; } = 0m;
    public decimal? DeclaredAmount { get; set; }
    public string? VarianceReason { get; set; }
    public decimal TotalCash { get; set; } = 0m;
    public decimal TotalCheque { get; set; } = 0m;
    public decimal Variance { get; set; } = 0m;
    public SettlementMode SettlementMode { get; set; } = SettlementMode.AT_REMITTANCE;
    public PayModel PayModel { get; set; } = PayModel.COMMISSION;
    public string? ProofName { get; set; }
    public string? ProofMimeType { get; set; }
    public string? ProofPath { get; set; }
    public string? ConfirmedById { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CourierClosedAt { get; set; }
    public string? CourierClosedById { get; set; }
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Repris de Wimo Laravel (`legacy_mappings`) : document historique, montants d'origine.</summary>
    public DateTime? ImportedAt { get; set; }
    public City City { get; set; } = null!;
    public Courier Courier { get; set; } = null!;
    public Agency Agency { get; set; } = null!;
    public User? ConfirmedBy { get; set; }
    public User? CourierClosedBy { get; set; }
    public ICollection<CashRemittanceParcel> Parcels { get; set; } = [];
    public ICollection<CourierBalanceEntry> Entries { get; set; } = [];
    public ICollection<Cheque> Cheques { get; set; } = [];
}
