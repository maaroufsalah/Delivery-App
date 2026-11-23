using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class ProductAttribute
{
    public string Id { get; set; } = Cuid.New();
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public ICollection<ProductAttributeValue> Values { get; set; } = [];
}
