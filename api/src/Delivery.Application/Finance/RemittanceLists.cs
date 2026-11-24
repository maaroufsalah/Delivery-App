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
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
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

internal static class RemittanceLists
{
    public static IQueryable<CashRemittance> Filter(IAppDbContext db, ICurrentUser caller, RemittanceListFilter filter)
    {
        var issues = new List<object>();
        if (filter.Status is { } s && !Remittances.Statuses.Contains(s)) issues.Add(new { path = "status", message = Zod.InvalidOption(Remittances.Statuses) });
        if (filter.Stage?.Any(x => !Remittances.Stages.Contains(x)) == true) issues.Add(new { path = "stage", message = Zod.InvalidOption(Remittances.Stages) });
        if (issues.Count > 0) throw BusinessException.Validation("Saisie invalide.", new { issues });
        var where = Remittances.Scoped(db.CashRemittances.AsNoTracking(), caller);
        if (filter.Stage is { Count: > 0 } stages) where = Remittances.InStages(where, stages);
        else if (filter.Status is { } status) { var value = Enum.Parse<CashRemittanceStatus>(Money.FromWire(status)); where = where.Where(r => r.Status == value); }
        if (filter.CourierId is { } couriers && caller.MayFilterBy(caller.CourierId)) where = where.Where(r => couriers.Contains(r.CourierId));
        if (filter.CityId is { } cities) where = where.Where(r => cities.Contains(r.CityId));
        if (filter.AgencyId is { } agencies && caller.MayFilterBy(caller.AgencyId)) where = where.Where(r => agencies.Contains(r.AgencyId));
        var (from, to) = ReturnRows.DayRange(filter.From, filter.To);
        if (from is { } f) where = where.Where(r => r.CreatedAt >= f);
        if (to is { } t) where = where.Where(r => r.CreatedAt < t);
        if (filter.Paging.Search is { Length: > 0 } search)
        {
            var needle = search.ToLower();
            where = where.Where(r => r.Code.ToLower().Contains(needle) || r.Courier.FirstName.ToLower().Contains(needle) || r.Courier.LastName.ToLower().Contains(needle)
                                     || r.Parcels.Any(l => l.Parcel.Code.ToLower().Contains(needle) || l.Parcel.RecipientName.ToLower().Contains(needle)
                                                           || l.Parcel.RecipientPhone.ToLower().Contains(needle) || l.Parcel.Client.Name.ToLower().Contains(needle))
                                     || r.Cheques.Any(c => c.Number.ToLower().Contains(needle)));
        }
        return filter.Paging.Order == "asc" ? where.OrderBy(r => r.CreatedAt) : where.OrderByDescending(r => r.CreatedAt);
    }
}
