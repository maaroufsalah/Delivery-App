namespace Delivery.Domain.Entities;

public partial class ParcelCodeSequence
{
    public DateOnly Day { get; set; }
    public string OriginCityId { get; set; } = string.Empty;
    public string DestinationCityId { get; set; } = string.Empty;
    public int LastRank { get; set; } = 0;
}
