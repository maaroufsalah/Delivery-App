namespace Delivery.Domain.Entities;

public partial class ZoneCity
{
    public string ZoneId { get; set; } = string.Empty;
    public string CityId { get; set; } = string.Empty;
    public Zone Zone { get; set; } = null!;
    public City City { get; set; } = null!;
}
