using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class ParcelFeeChange
{
    public string Id { get; set; } = Cuid.New();
    public string ParcelId { get; set; } = string.Empty;
    public ParcelFeeField Field { get; set; }
    public decimal? OldAmount { get; set; }
    public decimal NewAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Parcel Parcel { get; set; } = null!;
    public User? Author { get; set; }
}
