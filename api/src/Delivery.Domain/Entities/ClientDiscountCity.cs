namespace Delivery.Domain.Entities;

public partial class ClientDiscountCity
{
    public string DiscountId { get; set; } = string.Empty;
    public string CityId { get; set; } = string.Empty;
    public ClientDiscount Discount { get; set; } = null!;
    public City City { get; set; } = null!;
}
