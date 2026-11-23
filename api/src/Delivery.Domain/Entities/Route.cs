using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class Route
{
    public string Id { get; set; } = Cuid.New();
    public string Code { get; set; } = string.Empty;
    public RouteStatus Status { get; set; } = RouteStatus.DRAFT;
    public DateTime StatusAt { get; set; }
    public RouteKind Kind { get; set; } = RouteKind.DELIVERY;
    public string CityId { get; set; } = string.Empty;
    public string OriginCityId { get; set; } = string.Empty;
    public string CourierId { get; set; } = string.Empty;
    public DateTime? ExitDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public City City { get; set; } = null!;
    public City OriginCity { get; set; } = null!;
    public Courier Courier { get; set; } = null!;
    public ICollection<RouteParcel> Parcels { get; set; } = [];
    public ICollection<RouteNote> Notes { get; set; } = [];
}
