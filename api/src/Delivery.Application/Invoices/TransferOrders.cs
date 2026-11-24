using System.Globalization;
using System.Text.Json;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Invoices;

/// <summary>`TransferOrdersService` : un ordre de virement (OV-) vers les vendeurs, depuis une sélection de vendeurs ou des factures émises.</summary>
public static class TransferOrders
{
    public const string Prefix = "OV";

    public static async Task<TransferOrderDetailDto> DetailAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken)
    {
        var orders = db.TransferOrders.AsNoTracking().Where(o => o.Code == code);
        if (!caller.IsSuperAdmin()) { var agency = caller.AgencyId ?? Scope.DenyAllId; orders = orders.Where(o => o.AgencyId == agency); }
        var order = await orders.Select(o => new
        {
            o.Code, o.Source, o.Lines, o.InvoiceCodes, o.TotalCollected, o.TotalFees, o.TotalDue, o.CreatedAt,
            CreatedBy = o.CreatedBy != null ? o.CreatedBy.FirstName + " " + o.CreatedBy.LastName : null,
        }).FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound($"Ordre de virement introuvable : {code}");
        var lines = JsonSerializer.Deserialize<List<TransferLineDto>>(order.Lines, JsonSerializerOptions.Web) ?? [];
        return new TransferOrderDetailDto(order.Code, order.Source == TransferOrderSource.INVOICES ? "invoices" : "selection", lines, order.InvoiceCodes, Money.ToCents(order.TotalCollected),
            Money.ToCents(order.TotalFees), Money.ToCents(order.TotalDue), order.CreatedAt, order.CreatedBy);
    }

    public static string BaseName(TransferOrderDetailDto order) => $"Ordre-Virement-{order.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";

    /// <summary>`transferLinesForInvoices` : des factures émises au solde positif, dont aucun colis n'a déjà été viré.</summary>
    public static async Task<List<(TransferLineDto Line, List<string> ParcelIds)>> ForInvoicesAsync(IAppDbContext db, ICurrentUser caller, IReadOnlyList<string> codes,
        CancellationToken cancellationToken)
    {
        var invoices = await InvoiceRows.Scoped(db.Invoices.AsNoTracking(), caller).Where(i => codes.Contains(i.Code) && i.Status == InvoiceStatus.ISSUED && i.TotalDue > 0)
            .Select(i => new
            {
                i.Code, i.ClientId, Client = i.Client.Name, i.TotalDeliveryFee, i.TotalFailureFee, i.TotalAdjustment, i.TotalVat, i.TotalDue,
                Lines = i.Lines.Select(l => new { l.ParcelId, l.Parcel.Code, Orders = l.Parcel.TransferOrders.Select(t => t.TransferOrder.Code).ToList() }).ToList(),
            }).ToListAsync(cancellationToken);
        foreach (var invoice in invoices)
        {
            var transferred = invoice.Lines.Where(l => l.Orders.Count > 0).ToList();
            if (transferred.Count == 0) continue;
            var orders = transferred.SelectMany(l => l.Orders).Distinct().ToList();
            throw BusinessException.Conflict($"La facture {invoice.Code} porte {transferred.Count} colis déjà viré(s) par {string.Join(", ", orders)} : elle ne peut pas être virée une seconde fois.",
                new { reason = "invoice-parcels-already-transferred", invoiceCode = invoice.Code, transferOrders = orders, parcelCodes = transferred.Take(20).Select(l => l.Code).ToList() });
        }
        var byClient = new List<(TransferLineDto Line, List<string> ParcelIds)>();
        foreach (var invoice in invoices)
        {
            var fees = Money.ToCents(invoice.TotalDeliveryFee) + Money.ToCents(invoice.TotalFailureFee) + Money.ToCents(invoice.TotalAdjustment) + Money.ToCents(invoice.TotalVat);
            var due = Money.ToCents(invoice.TotalDue);
            var index = byClient.FindIndex(b => b.Line.ClientId == invoice.ClientId);
            var current = index < 0 ? (new TransferLineDto(invoice.ClientId, invoice.Client, 0, 0, 0, 0), new List<string>()) : byClient[index];
            current.Item2.AddRange(invoice.Lines.Select(l => l.ParcelId));
            var next = (current.Item1 with
            {
                ParcelCount = current.Item1.ParcelCount + invoice.Lines.Count, Fees = current.Item1.Fees + fees, Due = current.Item1.Due + due, Collected = current.Item1.Collected + due + fees,
            }, current.Item2);
            if (index < 0) byClient.Add(next);
            else byClient[index] = next;
        }
        return byClient.OrderBy(b => b.Line.ClientName, StringComparer.Create(CultureInfo.InvariantCulture, false)).ToList();
    }
}
