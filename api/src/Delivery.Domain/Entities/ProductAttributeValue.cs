using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class ProductAttributeValue
{
    public string Id { get; set; } = Cuid.New();
    public string AttributeId { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public ProductAttribute Attribute { get; set; } = null!;
    public ICollection<ProductAttributeAssignment> Assignments { get; set; } = [];
}
