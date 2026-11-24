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
using Delivery.Application.Finance;
using Delivery.Application.Notifications;
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
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Invoices;

internal static class PaymentLists
{
    public static IQueryable<Payment> Filter(IAppDbContext db, ICurrentUser caller, PaymentListFilter filter)
    {
        if (filter.Method?.Any(m => m is not ("cash" or "transfer" or "check")) == true)
        {
            throw BusinessException.Validation("Saisie invalide.", new { issues = new[] { new { path = "method", message = Zod.InvalidOption(["cash", "transfer", "check"]) } } });
        }
        var where = InvoiceRows.Payments(db.Payments.AsNoTracking(), caller);
        if (filter.ClientId is { } clients && caller.MayFilterBy(caller.ClientId)) where = where.Where(p => clients.Contains(p.ClientId));
        if (filter.AgencyId is { } agencies && caller.MayFilterBy(caller.AgencyId)) where = where.Where(p => agencies.Contains(p.AgencyId));
        if (!string.IsNullOrEmpty(filter.InvoiceId)) where = where.Where(p => p.InvoiceId == filter.InvoiceId);
        if (filter.Method is { } methods) { var values = methods.Select(m => Enum.Parse<PaymentMethod>(m.ToUpperInvariant())).ToList(); where = where.Where(p => values.Contains(p.Method)); }
        var (from, to) = ReturnRows.DayRange(filter.From, filter.To);
        if (from is { } f) where = where.Where(p => p.PaidAt >= f);
        if (to is { } t) where = where.Where(p => p.PaidAt < t);
        if (filter.Paging.Search is { Length: > 0 } search)
        {
            var needle = search.ToLower();
            where = where.Where(p => (p.Reference != null && p.Reference.ToLower().Contains(needle)) || (p.Invoice != null && p.Invoice.Code.ToLower().Contains(needle)) || p.Client.Name.ToLower().Contains(needle));
        }
        return filter.Paging.Order == "asc" ? where.OrderBy(p => p.PaidAt) : where.OrderByDescending(p => p.PaidAt);
    }
}
