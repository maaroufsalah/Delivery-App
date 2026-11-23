using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class Company
{
    public string Id { get; set; } = Cuid.New();
    public string Slug { get; set; } = string.Empty;
    public string? CustomDomain { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? LegalName { get; set; }
    public string? TaxId { get; set; }
    public string? Rc { get; set; }
    public string? ProLegalName { get; set; }
    public string? ProTaxId { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string Country { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
    public string DefaultLocale { get; set; } = string.Empty;
    public List<string> Locales { get; set; } = [];
    public string? LogoUrl { get; set; }
    public string? LogoBackground { get; set; }
    public string? IconUrl { get; set; }
    public string PrimaryColor { get; set; } = string.Empty;
    public string SecondaryColor { get; set; } = string.Empty;
    public string? Theme { get; set; }
    public CompanyStatus Status { get; set; } = CompanyStatus.TRIAL;
    public DateTime? TrialEndsAt { get; set; }
    public string? Plan { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
