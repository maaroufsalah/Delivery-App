namespace Delivery.Domain.Entities;

public partial class ParcelItem
{
    public string ParcelId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public Parcel Parcel { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
