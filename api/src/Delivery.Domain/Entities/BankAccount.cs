using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class BankAccount
{
    public string Id { get; set; } = Cuid.New();
    public string BankId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Rib { get; set; }
    public string? Iban { get; set; }
    public string? Holder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Bank Bank { get; set; } = null!;
    public ICollection<Payment> Payments { get; set; } = [];
    public ICollection<CodPayout> CodPayouts { get; set; } = [];
    public ICollection<CourierStatement> CourierStatements { get; set; } = [];
}
