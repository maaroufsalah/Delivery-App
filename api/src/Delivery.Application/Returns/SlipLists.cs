using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.CourierApp;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using Delivery.Domain.Pricing;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Returns;

internal static class SlipLists
{
    public static IQueryable<ReturnSlip> Filter(IAppDbContext db, ICurrentUser caller, SlipListFilter filter)
    {
        if (filter.Status is { } s && s is not ("draft" or "delivered"))
        {
            throw BusinessException.Validation("Saisie invalide.", new { issues = new[] { new { path = "status", message = Zod.InvalidOption(["draft", "delivered"]) } } });
        }
        var where = ReturnRows.Slips(db.ReturnSlips.AsNoTracking(), caller);
        if (filter.Status is { } status) { var value = status == "draft" ? ReturnSlipStatus.DRAFT : ReturnSlipStatus.DELIVERED; where = where.Where(x => x.Status == value); }
        if (filter.ClientId is { } clients && caller.MayFilterBy(caller.ClientId)) where = where.Where(x => clients.Contains(x.ClientId));
        if (filter.AgencyId is { } agencies && caller.MayFilterBy(caller.AgencyId)) where = where.Where(x => agencies.Contains(x.AgencyId));
        var (from, to) = ReturnRows.DayRange(filter.From, filter.To);
        if (from is { } f) where = where.Where(x => x.CreatedAt >= f);
        if (to is { } t) where = where.Where(x => x.CreatedAt < t);
        if (filter.Paging.Search is { Length: > 0 } search)
        {
            var needle = search.ToLower();
            where = where.Where(x => x.Code.ToLower().Contains(needle) || x.Client.Name.ToLower().Contains(needle) || x.Items.Any(i => i.Parcel.Code.ToLower().Contains(needle)));
        }
        // Le vendeur ne voit pas les brouillons vides que l'agence prépare.
        if (caller.ClientId is not null && !caller.IsSuperAdmin()) where = where.Where(x => !(x.Status == ReturnSlipStatus.DRAFT && !x.Items.Any()));
        return filter.Paging.Order == "desc" ? where.OrderByDescending(x => x.CreatedAt) : where.OrderBy(x => x.CreatedAt);
    }
}
