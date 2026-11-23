namespace Delivery.Domain.Entities;

public partial class CourierCity
{
    public string CourierId { get; set; } = string.Empty;
    public string CityId { get; set; } = string.Empty;
    public decimal PickupFee { get; set; }
    public decimal DeliveryFee { get; set; }
    public Courier Courier { get; set; } = null!;
    public City City { get; set; } = null!;
}
