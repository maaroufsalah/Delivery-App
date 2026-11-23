using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class Setting
{
    public string Id { get; set; } = Cuid.New();
    public SettingScope Scope { get; set; } = SettingScope.COMPANY;
    public string? AgencyId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
