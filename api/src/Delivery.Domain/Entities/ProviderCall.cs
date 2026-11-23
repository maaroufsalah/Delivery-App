using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class ProviderCall
{
    public string Id { get; set; } = Cuid.New();
    public string TokenId { get; set; } = string.Empty;
    public string? EventKey { get; set; }
    public string ParcelCode { get; set; } = string.Empty;
    public string? ParcelId { get; set; }
    public int ProviderStatus { get; set; }
    public int? MappedStatus { get; set; }
    public DateTime? OccurredAt { get; set; }
    public ProviderMode Mode { get; set; }
    public ProviderCallOutcome Outcome { get; set; }
    public int HttpStatus { get; set; }
    public string? Reason { get; set; }
    public string? RequestId { get; set; }
    public bool Alert { get; set; } = false;
    public DateTime? AlertResolvedAt { get; set; }
    public DateTime At { get; set; }
    public ProviderToken Token { get; set; } = null!;
}
