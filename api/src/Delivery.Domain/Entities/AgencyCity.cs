namespace Delivery.Domain.Entities;

public partial class AgencyCity
{
    public string AgencyId { get; set; } = string.Empty;
    public string CityId { get; set; } = string.Empty;
    public Agency Agency { get; set; } = null!;
    public City City { get; set; } = null!;
}
