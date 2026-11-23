using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class Payment
{
    public string Id { get; set; } = Cuid.New();
    public string? InvoiceId { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string AgencyId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.CASH;
    public string? BankAccountId { get; set; }
    public string? Reference { get; set; }
    public string? Note { get; set; }
    public DateTime PaidAt { get; set; }
    public string? ProofName { get; set; }
    public string? ProofMimeType { get; set; }
    public string? ProofPath { get; set; }
    public string? ChequeNumber { get; set; }
    public string? ChequeBank { get; set; }
    public DateOnly? ChequeDate { get; set; }
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Invoice? Invoice { get; set; }
    public Client Client { get; set; } = null!;
    public Agency Agency { get; set; } = null!;
    public BankAccount? BankAccount { get; set; }
}
