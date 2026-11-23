using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class ChequeEvent
{
    public string Id { get; set; } = Cuid.New();
    public string ChequeId { get; set; } = string.Empty;
    public ChequeStatus Status { get; set; }
    public string Kind { get; set; } = "transition";
    public string? Comment { get; set; }
    public string? AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public DateTime CreatedAt { get; set; }
    public Cheque Cheque { get; set; } = null!;
    public User? Author { get; set; }
}
