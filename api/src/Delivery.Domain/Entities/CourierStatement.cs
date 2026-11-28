using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class CourierStatement
{
    public string Id { get; set; } = Cuid.New();
    public string Code { get; set; } = string.Empty;
    public CourierStatementStatus Status { get; set; } = CourierStatementStatus.DRAFT;
    public string CourierId { get; set; } = string.Empty;
    public string AgencyId { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public PayPeriod PeriodKind { get; set; } = PayPeriod.MONTHLY;
    public PayModel PayModel { get; set; } = PayModel.COMMISSION;
    public SettlementMode SettlementMode { get; set; } = SettlementMode.AT_REMITTANCE;
    public decimal BaseAmount { get; set; } = 0m;
    public decimal CodCollected { get; set; } = 0m;
    public decimal CommissionsTotal { get; set; } = 0m;
    public decimal CommissionsDeducted { get; set; } = 0m;
    public decimal VariableAmount { get; set; } = 0m;
    public decimal TotalDue { get; set; } = 0m;
    public string? Note { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public string? ValidatedById { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? PaidById { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CASH;
    public string? BankAccountId { get; set; }
    public string? Reference { get; set; }
    public string? ChequeNumber { get; set; }
    public string? ChequeBank { get; set; }
    public DateOnly? ChequeDate { get; set; }
    public string? ProofName { get; set; }
    public string? ProofMimeType { get; set; }
    public string? ProofPath { get; set; }
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Repris de Wimo Laravel (`legacy_mappings`) : document historique, montants d'origine.</summary>
    public DateTime? ImportedAt { get; set; }
    public Courier Courier { get; set; } = null!;
    public Agency Agency { get; set; } = null!;
    public User? ValidatedBy { get; set; }
    public User? PaidBy { get; set; }
    public BankAccount? BankAccount { get; set; }
    public ICollection<CourierStatementLine> Lines { get; set; } = [];
}
