using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;

namespace Delivery.Application.Settings.Dtos;

/// <summary>`CompanySettings` : identité, marque et thème de la société.</summary>
public sealed record CompanySettingsDto(
    string Id,
    string Name,
    string ShortName,
    string Logo,
    string? LogoBackground,
    string? Icon,
    string PrimaryColor,
    string SecondaryColor,
    string Currency,
    string Country,
    string TimeZone,
    IReadOnlyList<string> Locales,
    string DefaultLocale,
    string? LegalName,
    string? TaxId,
    string? Rc,
    string? ProLegalName,
    string? ProTaxId,
    string? Address,
    string? City,
    string? Phone,
    string? Email,
    string? Website,
    JsonObject Theme)
{
    /// <summary>Clés en plus, en fin d'objet : `environmentLabel` sur `/public/company` seulement.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; init; }

    public const string Placeholder = "/brands/placeholder.svg";
    public const int ShortNameMax = 20;

    /// <summary>`companyShortName`.</summary>
    public static string ShortNameOf(Company company)
    {
        var shortName = company.ShortName?.Trim();
        if (!string.IsNullOrEmpty(shortName)) return shortName;
        var name = company.Name.Trim();
        return name.Length > ShortNameMax ? name[..ShortNameMax] : name;
    }

    public static CompanySettingsDto From(Company company) => new(
        company.Id, company.Name, ShortNameOf(company), company.LogoUrl ?? Placeholder, company.LogoBackground, company.IconUrl,
        company.PrimaryColor, company.SecondaryColor, company.Currency, company.Country, company.Timezone, company.Locales, company.DefaultLocale,
        company.LegalName, company.TaxId, company.Rc, company.ProLegalName, company.ProTaxId, company.Address, company.City, company.Phone,
        company.Email, company.Website, ThemeOverrides.Prune(company.Theme));
}

/// <summary>`BusinessSettings`.</summary>
public sealed record BusinessSettingsDto(
    string ParcelCodePrefix, IReadOnlyList<string> LocalesEnabled, string LocalesDefault, double ClaimResolutionDays,
    string LabelTemplate, string LabelBrandColor, string LabelBanner);

/// <summary>`FinanceSettings` : montants en MAD, taux en pourcent.</summary>
public sealed record FinanceSettingsDto(
    double VatRate, double ProVatRate, string InvoicePrefix, string RemittancePrefix, string StatementPrefix, string CodPayoutPrefix,
    bool NumberingYearlyReset, double FailureFeeSameCity, double FailureFeeCrossCity, double PickupFee, long FreePickupThreshold,
    double CourierReturnFee, double CourierFailedCommission, string CourierPayPeriod, string InvoiceFooter, string CodPayoutFooter,
    string? DefaultBankAccountId, double AuditRetentionDays, int UploadsRetentionDays);

/// <summary>Atelier du thème : palettes proposées et instantanés « standard » / « précédent ».</summary>
public sealed record ThemeWorkbenchDto(JsonNode Palettes, JsonNode? Standard, JsonNode? Previous);
