using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class CourierStatementLine
{
    public string Id { get; set; } = Cuid.New();
    public string StatementId { get; set; } = string.Empty;
    public string ParcelId { get; set; } = string.Empty;
    public CourierStatementLineKind Kind { get; set; }
    public string ParcelCode { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;
    public int StatusId { get; set; }
    public decimal Amount { get; set; }
    public bool Deducted { get; set; } = false;
    public DateTime WorkedAt { get; set; }
    public string? SettledParcelId { get; set; }
    public DateTime CreatedAt { get; set; }
    public CourierStatement Statement { get; set; } = null!;
    public Parcel Parcel { get; set; } = null!;
}
