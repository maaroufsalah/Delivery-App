namespace Delivery.Application.Merchant.Dtos;

public sealed record PickupTermsDto(long Fee, long FreeFrom);

/// <summary>`MerchantCompany` : la fiche vue par le vendeur, frais de ramassage effectifs compris.</summary>
public sealed record MerchantCompanyDto(string Name, string Phone, string Email, string CityId, string CityName, string? Address, bool IsPro, string BillingType, PickupTermsDto PickupTerms);
