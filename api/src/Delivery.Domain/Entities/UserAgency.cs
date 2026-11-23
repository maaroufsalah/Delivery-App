namespace Delivery.Domain.Entities;

public partial class UserAgency
{
    public string UserId { get; set; } = string.Empty;
    public string AgencyId { get; set; } = string.Empty;
    public User User { get; set; } = null!;
    public Agency Agency { get; set; } = null!;
}
