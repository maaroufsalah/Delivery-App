using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.Parcels;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Finance;

public sealed record BankDto(string Id, string Name, string? Code, bool IsActive);

public sealed record BankAccountDto(string Id, string BankId, string BankName, string Label, string? Rib, string? Holder, bool IsActive, bool IsDefault);

public sealed record CashToRemitDto(string CourierId, string CourierName, int ParcelCount, long Amount);

public sealed record CountAmountDto(int ParcelCount, long Amount);

public sealed record UnpaidInvoicesDto(int Count, long Amount, int OverdueCount, DateTime? OldestIssuedAt, int? OldestAgeDays);

public sealed record FinanceDashboardDto(IReadOnlyList<CashToRemitDto> CashToRemit, long CashToRemitTotal, CountAmountDto RemittedNotInvoiced, UnpaidInvoicesDto UnpaidInvoices,
    long FeeRevenueThisMonth);

public sealed record OverviewCashDto(long Collected, long Cash, long Cheque, long Commissions, long Expected, long Handed, long Variance, int OpenCount, int ValidatedCount);

public sealed record OverviewBalanceRowDto(string CourierId, string CourierName, long Balance);

public sealed record OverviewBalancesDto(long Total, IReadOnlyList<OverviewBalanceRowDto> Rows);

public sealed record CountSumDto(int Count, long Amount);

public sealed record OverviewChequesDto(CountSumDto Received, CountSumDto Pending, CountSumDto Cashed, CountSumDto Rejected);

public sealed record OverviewFinancialStatusDto(string Status, int Count, long Amount);

public sealed record OverviewInvoicesDto(CountSumDto Open, CountSumDto Paid);

public sealed record OverviewStatementsDto(CountSumDto Open, CountSumDto Validated);

public sealed record FinanceOverviewDto(string Period, string From, string To, string? CourierId, OverviewCashDto Cash, OverviewBalancesDto Balances, OverviewChequesDto Cheques,
    IReadOnlyList<OverviewFinancialStatusDto> ParcelsByFinancialStatus, int DeliveredCount, OverviewInvoicesDto ClientInvoices, OverviewStatementsDto CourierStatements);
