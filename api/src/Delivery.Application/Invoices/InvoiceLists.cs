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

internal static class InvoiceLists
{
    private static readonly System.Text.RegularExpressions.Regex IsoDay = new(@"^\d{4}-\d{2}-\d{2}$");

    public static void Validate(InvoiceListFilter filter)
    {
        var issues = new List<object>();
        if (filter.Status?.Any(s => !InvoiceRows.Statuses.Contains(s)) == true) issues.Add(new { path = "status", message = Zod.InvalidOption(InvoiceRows.Statuses) });
        if (filter.IssuedDay is { } day && !IsoDay.IsMatch(day)) issues.Add(new { path = "issuedDay", message = "Invalid string: must match pattern /^\\d{4}-\\d{2}-\\d{2}$/" });
        if (issues.Count > 0) throw BusinessException.Validation("Saisie invalide.", new { issues });
    }

    /// <summary>`listWhere`.</summary>
    public static IQueryable<Invoice> Where(IAppDbContext db, ICurrentUser caller, InvoiceListFilter filter)
    {
        Validate(filter);
        var where = InvoiceRows.Scoped(db.Invoices.AsNoTracking(), caller);
        if (filter.Kind is { } kind) { var pro = kind == "pro"; where = where.Where(i => i.Client.IsPro == pro); }
        if (filter.Status is { } statuses) { var values = statuses.Select(s => Enum.Parse<InvoiceStatus>(s.ToUpperInvariant())).ToList(); where = where.Where(i => values.Contains(i.Status)); }
        if (filter.ClientId is { } clients && caller.MayFilterBy(caller.ClientId)) where = where.Where(i => clients.Contains(i.ClientId));
        if (filter.AgencyId is { } agencies && caller.MayFilterBy(caller.AgencyId)) where = where.Where(i => agencies.Contains(i.AgencyId));
        if (filter.UnpaidOnly) where = where.Where(i => i.Status == InvoiceStatus.DRAFT || i.Status == InvoiceStatus.ISSUED);
        var (from, to) = ReturnRows.DayRange(filter.From, filter.To);
        if (from is { } f) where = where.Where(i => i.CreatedAt >= f);
        if (to is { } t) where = where.Where(i => i.CreatedAt < t);
        if (filter.IssuedDay is { } issued)
        {
            var (start, end) = ReturnRows.DayRange(issued, issued);
            where = where.Where(i => i.IssuedAt >= start && i.IssuedAt < end);
        }
        if (filter.Paging.Search is { Length: > 0 } search)
        {
            var needle = search.ToLower();
            where = where.Where(i => i.Code.ToLower().Contains(needle) || i.Client.Name.ToLower().Contains(needle));
        }
        return where;
    }

    public static IQueryable<Invoice> Ordered(IQueryable<Invoice> where, ListQuery paging)
    {
        var asc = paging.Order == "asc";
        if (paging.Sort == "issuedAt")
        {
            return asc
                ? where.OrderBy(i => i.IssuedAt != null).ThenBy(i => i.IssuedAt).ThenBy(i => i.CreatedAt).ThenBy(i => i.Code)
                : where.OrderBy(i => i.IssuedAt != null).ThenByDescending(i => i.IssuedAt).ThenByDescending(i => i.CreatedAt).ThenByDescending(i => i.Code);
        }
        return asc ? where.OrderBy(i => i.CreatedAt) : where.OrderByDescending(i => i.CreatedAt);
    }

    public static readonly IReadOnlyList<ExportColumn<InvoiceListItemDto>> Columns =
    [
        new("Facture", r => r.Code),
        new("Statut", r => DocumentLabels.Enum(r.Status)),
        new("E-commerçant", r => r.ClientName),
        new("Agence", r => r.AgencyName),
        new("Colis", r => r.ParcelCount),
        new("Total COD", r => Exports.Amount(r.TotalCod)),
        new("Frais de livraison HT", r => Exports.Amount(r.TotalDeliveryFee)),
        new("Frais d’échec HT", r => Exports.Amount(r.TotalFailureFee)),
        new("Ajustements HT", r => Exports.Amount(r.TotalAdjustment)),
        new("TVA", r => Exports.Amount(r.TotalVat)),
        new("Net à reverser", r => Exports.Amount(r.TotalDue)),
        new("Réglé", r => Exports.Amount(r.TotalPaid)),
        new("Émise le", r => Exports.DateTimeText(r.IssuedAt)),
        new("Échéance", r => r.DueAt is { } due ? Exports.CalendarDateText(DateOnly.FromDateTime(due)) : string.Empty),
        new("Soldée le", r => Exports.DateTimeText(r.PaidAt)),
    ];

    public static readonly IReadOnlyList<ExportColumn<InvoiceListItemDto>> ProColumns =
    [
        .. Columns,
        new("Taux de TVA (%)", r => (r.ProVatRate ?? r.VatRate) / 100m),
        new("Total HT", r => Exports.Amount(r.TotalDeliveryFee + r.TotalFailureFee + r.TotalAdjustment)),
        new("Total TTC", r => Exports.Amount(r.TotalDeliveryFee + r.TotalFailureFee + r.TotalAdjustment + r.TotalVat)),
    ];
}
