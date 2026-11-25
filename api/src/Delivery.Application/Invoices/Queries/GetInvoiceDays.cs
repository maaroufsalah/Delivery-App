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

/// <summary>`invoiceDaysQuerySchema` : par jour d'émission (ou brouillons), le compte, le dû, les règlements, les codes encore à virer.</summary>
public sealed record GetInvoiceDaysQuery(InvoiceListFilter Filter, string? Days) : IRequest<IReadOnlyList<InvoiceDaySummaryDto>>;

internal sealed class GetInvoiceDaysHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetInvoiceDaysQuery, IReadOnlyList<InvoiceDaySummaryDto>>
{
    public async Task<IReadOnlyList<InvoiceDaySummaryDto>> Handle(GetInvoiceDaysQuery request, CancellationToken cancellationToken)
    {
        if (request.Days is null) throw BusinessException.Validation("Saisie invalide.", new { issues = new[] { new { path = "days", message = Zod.Expected("string") } } });
        var days = request.Days.Trim().Split(',').Select(d => d.Trim()).Where(d => d.Length > 0).Distinct().ToList();
        if (days.Count > 62 || days.Any(d => d != InvoiceRows.DraftDay && !System.Text.RegularExpressions.Regex.IsMatch(d, @"^\d{4}-\d{2}-\d{2}$")))
        {
            throw BusinessException.Validation("Saisie invalide.", new { issues = new[] { new { path = "days", message = "Invalid input" } } });
        }
        if (days.Count == 0) return [];
        var where = InvoiceLists.Where(db, caller, request.Filter);
        var calendar = days.Where(d => d != InvoiceRows.DraftDay).Order(StringComparer.Ordinal).ToList();
        var wantsDraft = days.Contains(InvoiceRows.DraftDay);
        DateTime? start = null, end = null;
        if (calendar.Count > 0) (start, end) = ReturnRows.DayRange(calendar[0], calendar[^1]);
        var hasCalendar = calendar.Count > 0;
        var rows = await where.Where(i => (wantsDraft && i.IssuedAt == null) || (hasCalendar && i.IssuedAt >= start && i.IssuedAt < end))
            .Select(i => new { i.Code, i.IssuedAt, i.TotalDue, Payments = i.Payments.Select(p => new { p.Amount, p.Method }).ToList(), Lines = i.Lines.Count,
                // Un colis déjà viré (estampillé par un OV, même « sélection ») : la facture l'est aussi.
                Stamped = i.Lines.Any(l => l.Parcel.TransferOrders.Any()) }).ToListAsync(cancellationToken);
        var summaries = new Dictionary<string, (int Invoices, int Parcels, long Due, long Cash, long Cheque, long Transfer, List<string> Codes)>();
        var issued = new List<string>();
        foreach (var row in rows)
        {
            var day = row.IssuedAt is { } at ? Periods.LocalDay(at, ClockDays.Zone) : InvoiceRows.DraftDay;
            if (!days.Contains(day)) continue;
            var s = summaries.GetValueOrDefault(day, (0, 0, 0, 0, 0, 0, new List<string>()));
            s.Invoices++;
            s.Parcels += row.Lines;
            var due = Money.ToCents(row.TotalDue);
            s.Due += due;
            // Un règlement est saisi en positif ; il prend le sens de sa facture (encaissé d'un vendeur à net négatif = −).
            var (cash, cheque, transfer) = TransferRules.PaymentSplit(due, row.Payments.Select(p => (p.Method, Money.ToCents(p.Amount))));
            s.Cash += cash;
            s.Cheque += cheque;
            s.Transfer += transfer;
            if (day != InvoiceRows.DraftDay && TransferRules.IsTransferable(due, row.Stamped, false)) { s.Codes.Add(row.Code); issued.Add(row.Code); }
            summaries[day] = s;
        }
        var transferred = new HashSet<string>();
        if (issued.Count > 0)
        {
            var orders = await db.TransferOrders.AsNoTracking().Where(o => o.InvoiceCodes.Any(c => issued.Contains(c))).Select(o => o.InvoiceCodes).ToListAsync(cancellationToken);
            foreach (var codes in orders) transferred.UnionWith(codes);
        }
        return days.Where(summaries.ContainsKey).Select(d =>
        {
            var s = summaries[d];
            return new InvoiceDaySummaryDto(d, s.Invoices, s.Parcels, s.Due, s.Cash, s.Cheque, s.Transfer, s.Codes.Where(c => !transferred.Contains(c)).Order(StringComparer.Ordinal).ToList());
        }).ToList();
    }
}
