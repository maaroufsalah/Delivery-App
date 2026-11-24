using Delivery.Api.Auth;
using Delivery.Application.Finance;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Delivery.Api.Controllers;

/// <summary>`FinanceDashboardController` : le tableau financier de la société, hors vendeur.</summary>
[Route("finance")]
[Guards(Guard.NotMerchant)]
public sealed class FinanceDashboardController(ISender sender) : ApiControllerBase(sender)
{
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [HttpGet("dashboard")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Dashboard([FromQuery] string? agencyId) => Ok(new GetFinanceDashboardQuery(Clean(agencyId)));

    [HttpGet("overview")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Overview([FromQuery] string? period, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? courierId, [FromQuery] string? agencyId) =>
        Ok(new GetFinanceOverviewQuery(period, Clean(from), Clean(to), Clean(courierId), Clean(agencyId)));
}

/// <summary>`BanksController` : le référentiel bancaire en lecture (les comptes de la société hors vendeur).</summary>
[Route("")]
public sealed class BanksController(ISender sender) : ApiControllerBase(sender)
{
    [HttpGet("banks")]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Banks() => Ok(new GetBanksQuery());

    [HttpGet("bank-accounts")]
    [Guards(Guard.NotMerchant)]
    [RequirePermission("finance.read")]
    public Task<IActionResult> Accounts() => Ok(new GetBankAccountsQuery());
}
