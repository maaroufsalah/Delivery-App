using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class ClaimType
{
    public string Id { get; set; } = Cuid.New();
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public ClaimSubjectType SubjectType { get; set; } = ClaimSubjectType.PARCEL;
    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public ICollection<Claim> Claims { get; set; } = [];
}
