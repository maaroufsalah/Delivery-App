using System.Globalization;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Returns;

internal static class HandoverLists
{
    public static IQueryable<ReturnHandover> Filter(IAppDbContext db, ICurrentUser caller, HandoverListFilter filter)
    {
        if (filter.Status?.FirstOrDefault(s => !ReturnRows.HandoverStatuses.Contains(s)) is not null)
        {
            throw BusinessException.Validation("Saisie invalide.", new { issues = new[] { new { path = "status", message = Zod.InvalidOption(ReturnRows.HandoverStatuses) } } });
        }
        if (filter.Origin is { } o && !ReturnRows.HandoverOrigins.Contains(o))
        {
            throw BusinessException.Validation("Saisie invalide.", new { issues = new[] { new { path = "origin", message = Zod.InvalidOption(ReturnRows.HandoverOrigins) } } });
        }
        var where = ReturnRows.Handovers(db.ReturnHandovers.AsNoTracking(), caller);
        if (filter.Status is { } statuses)
        {
            var values = statuses.Select(s => Enum.Parse<ReturnHandoverStatus>(Money.FromWire(s))).ToList();
            where = where.Where(h => values.Contains(h.Status));
        }
        if (filter.Origin is { } origin) { var value = Enum.Parse<ReturnHandoverOrigin>(Money.FromWire(origin)); where = where.Where(h => h.Origin == value); }
        if (filter.CourierId is { } couriers && caller.MayFilterBy(caller.CourierId)) where = where.Where(h => couriers.Contains(h.CourierId));
        if (filter.AgencyId is { } agencies && caller.MayFilterBy(caller.AgencyId)) where = where.Where(h => agencies.Contains(h.AgencyId));
        var (from, to) = ReturnRows.DayRange(filter.From, filter.To);
        if (from is { } f) where = where.Where(h => h.CreatedAt >= f);
        if (to is { } t) where = where.Where(h => h.CreatedAt < t);
        if (filter.Paging.Search is { Length: > 0 } search)
        {
            var needle = search.ToLower();
            where = where.Where(h => h.Code.ToLower().Contains(needle) || h.Courier.LastName.ToLower().Contains(needle));
        }
        return filter.Paging.Order == "desc" ? where.OrderByDescending(h => h.CreatedAt) : where.OrderBy(h => h.CreatedAt);
    }
}
