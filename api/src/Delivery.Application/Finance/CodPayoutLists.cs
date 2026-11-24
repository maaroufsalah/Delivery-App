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
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Application.PettyCash;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Finance;

internal static class CodPayoutLists
{
    public static IQueryable<CodPayout> Filter(IAppDbContext db, ICurrentUser caller, CodPayoutListFilter filter)
    {
        if (filter.Status is { } s && s is not ("draft" or "paid"))
        {
            throw BusinessException.Validation("Saisie invalide.", new { issues = new[] { new { path = "status", message = Zod.InvalidOption(["draft", "paid"]) } } });
        }
        var where = CodPayouts.Scoped(db.CodPayouts.AsNoTracking(), caller);
        if (caller.ClientId is not null && !caller.IsSuperAdmin()) where = where.Where(p => p.Client.BillingType == BillingType.PROFESSIONAL);
        if (filter.Status is { } status) { var value = status == "draft" ? CodPayoutStatus.DRAFT : CodPayoutStatus.PAID; where = where.Where(p => p.Status == value); }
        if (filter.ClientId is { } clients && caller.MayFilterBy(caller.ClientId)) where = where.Where(p => clients.Contains(p.ClientId));
        if (filter.AgencyId is { } agencies && caller.MayFilterBy(caller.AgencyId)) where = where.Where(p => agencies.Contains(p.AgencyId));
        var (from, to) = ReturnRows.DayRange(filter.From, filter.To);
        if (from is { } f) where = where.Where(p => p.CreatedAt >= f);
        if (to is { } t) where = where.Where(p => p.CreatedAt < t);
        if (filter.Paging.Search is { Length: > 0 } search)
        {
            var needle = search.ToLower();
            where = where.Where(p => p.Code.ToLower().Contains(needle) || (p.Reference != null && p.Reference.ToLower().Contains(needle)) || p.Client.Name.ToLower().Contains(needle));
        }
        return filter.Paging.Order == "asc" ? where.OrderBy(p => p.CreatedAt) : where.OrderByDescending(p => p.CreatedAt);
    }
}
