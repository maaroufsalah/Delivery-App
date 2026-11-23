using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class ParcelTransition
{
    public int FromStatusId { get; set; }
    public int ToStatusId { get; set; }
    public TransitionKind Kind { get; set; } = TransitionKind.NOMINAL;
    public ParcelStatus FromStatus { get; set; } = null!;
    public ParcelStatus ToStatus { get; set; } = null!;
}
