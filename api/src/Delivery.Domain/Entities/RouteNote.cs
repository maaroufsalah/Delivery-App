namespace Delivery.Domain.Entities;

public partial class RouteNote
{
    public string RouteId { get; set; } = string.Empty;
    public string DeliveryNoteId { get; set; } = string.Empty;
    public int Sequence { get; set; } = 0;
    public Route Route { get; set; } = null!;
    public DeliveryNote DeliveryNote { get; set; } = null!;
}
