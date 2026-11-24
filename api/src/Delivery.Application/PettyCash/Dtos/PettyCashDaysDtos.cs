using System.Globalization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.PettyCash;

public sealed record PettyCashDaySummaryDto(string Day, string? AgencyId, string? AgencyName, int Agencies, long OpeningBalance, long Supplied, long Spent, long ClosingBalance, int PendingDrafts,
    string Status, DateTime? ClosedAt, string? ClosedBy, DateTime? ReopenedAt, string? ReopenedByName, string? ReopenReason);

public sealed record PettyCashDaysPageDto(IReadOnlyList<PettyCashDaySummaryDto> Items, string Today, PettyCashDaySummaryDto? Current);

public sealed record PettyCashDayDetailDto(PettyCashDaySummaryDto Summary, IReadOnlyList<PettyCashDaySummaryDto> Agencies);
