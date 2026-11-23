using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class Order
{
    public string Id { get; set; } = Cuid.New();
    public string Code { get; set; } = string.Empty;
    public OrderStatus Status { get; set; } = OrderStatus.DRAFT;
    public OrderSource Source { get; set; } = OrderSource.MANUAL;
    public string? ExternalRef { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string? AgencyId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientPhone { get; set; } = string.Empty;
    public string DestinationCityId { get; set; } = string.Empty;
    public string? DestinationDistrictId { get; set; }
    public string? DestinationDistrictName { get; set; }
    public string DeliveryAddress { get; set; } = string.Empty;
    public string? Note { get; set; }
    public decimal TotalAmount { get; set; } = 0m;
    public string? ParcelId { get; set; }
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Client Client { get; set; } = null!;
    public Agency? Agency { get; set; }
    public City DestinationCity { get; set; } = null!;
    public District? DestinationDistrict { get; set; }
    public Parcel? Parcel { get; set; }
    public ICollection<OrderLine> Lines { get; set; } = [];
}
