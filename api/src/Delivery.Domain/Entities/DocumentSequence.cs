namespace Delivery.Domain.Entities;

public partial class DocumentSequence
{
    public string Kind { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public int LastRank { get; set; } = 0;
}
