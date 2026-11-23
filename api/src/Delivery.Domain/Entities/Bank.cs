using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class Bank
{
    public string Id { get; set; } = Cuid.New();
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public ICollection<BankAccount> Accounts { get; set; } = [];
}
