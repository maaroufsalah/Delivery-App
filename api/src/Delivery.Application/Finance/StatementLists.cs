using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.PettyCash;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Finance;

internal static class StatementLists
{
    public static readonly string[] Statuses = ["draft", "validated", "paid"];

    public static IQueryable<CourierStatement> Filter(IAppDbContext db, ICurrentUser caller, StatementListFilter filter)
    {
        if (filter.Status is { } s && !Statuses.Contains(s))
        {
            throw BusinessException.Validation("Saisie invalide.", new { issues = new[] { new { path = "status", message = Zod.InvalidOption(Statuses) } } });
        }
        var where = CourierStatements.Scoped(db.CourierStatements.AsNoTracking(), caller);
        if (filter.Status is { } status) { var value = Enum.Parse<CourierStatementStatus>(status.ToUpperInvariant()); where = where.Where(x => x.Status == value); }
        if (!string.IsNullOrEmpty(filter.CourierId) && caller.MayFilterBy(caller.CourierId)) where = where.Where(x => x.CourierId == filter.CourierId);
        if (!string.IsNullOrEmpty(filter.AgencyId) && caller.MayFilterBy(caller.AgencyId)) where = where.Where(x => x.AgencyId == filter.AgencyId);
        if (!string.IsNullOrEmpty(filter.From)) { var from = Day(filter.From); where = where.Where(x => x.PeriodEnd >= from); }
        if (!string.IsNullOrEmpty(filter.To)) { var to = Day(filter.To).AddDays(1).AddMilliseconds(-1); where = where.Where(x => x.PeriodStart <= to); }
        if (filter.Paging.Search is { Length: > 0 } search)
        {
            var needle = search.ToLower();
            where = where.Where(x => x.Code.ToLower().Contains(needle) || x.Courier.LastName.ToLower().Contains(needle));
        }
        return filter.Paging.Order == "asc" ? where.OrderBy(x => x.PeriodStart) : where.OrderByDescending(x => x.PeriodStart);
    }

    public static DateTime Day(string day) => DateTime.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date)
        ? date
        : throw BusinessException.Validation("Saisie invalide.", new { issues = new[] { new { path = "from", message = "Invalid date" } } });
}
