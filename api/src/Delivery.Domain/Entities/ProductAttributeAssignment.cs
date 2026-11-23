namespace Delivery.Domain.Entities;

public partial class ProductAttributeAssignment
{
    public string ProductId { get; set; } = string.Empty;
    public string ValueId { get; set; } = string.Empty;
    public Product Product { get; set; } = null!;
    public ProductAttributeValue Value { get; set; } = null!;
}
