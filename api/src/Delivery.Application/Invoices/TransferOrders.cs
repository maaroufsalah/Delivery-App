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
using Delivery.Domain.Finance;
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

    /// <summary>
    /// `transferLinesForInvoices` : des factures émises à net ≥ 0 (ou payées à net 0, nées soldées), dont aucun colis n'a déjà
    /// été viré. Chaque ligne porte les factures qu'elle vire réellement : l'OV n'enregistre qu'elles.
    /// </summary>
    public static async Task<List<(TransferLineDto Line, List<string> ParcelIds, IReadOnlyList<string> InvoiceCodes)>> ForInvoicesAsync(IAppDbContext db, ICurrentUser caller,
        IReadOnlyList<string> codes, CancellationToken cancellationToken)
    {
        var candidates = await InvoiceRows.Scoped(db.Invoices.AsNoTracking(), caller)
            .Where(i => codes.Contains(i.Code) && (i.Status == InvoiceStatus.ISSUED || i.Status == InvoiceStatus.PAID) && i.TotalDue >= 0)
            .Select(i => new
            {
                i.Code, i.Status, i.ClientId, Client = i.Client.Name, i.TotalDeliveryFee, i.TotalFailureFee, i.TotalAdjustment, i.TotalVat, i.TotalDue,
                Lines = i.Lines.Select(l => new { l.ParcelId, l.Parcel.Code, Orders = l.Parcel.TransferOrders.Select(t => t.TransferOrder.Code).ToList() }).ToList(),
            }).ToListAsync(cancellationToken);
        var invoices = candidates.Where(i => TransferRules.EntersTransferOrder(i.Status, Money.ToCents(i.TotalDue))).ToList();
        foreach (var invoice in invoices)
        {
            var transferred = invoice.Lines.Where(l => l.Orders.Count > 0).ToList();
            if (transferred.Count == 0) continue;
            var orders = transferred.SelectMany(l => l.Orders).Distinct().ToList();
            throw BusinessException.Conflict($"La facture {invoice.Code} porte {transferred.Count} colis déjà viré(s) par {string.Join(", ", orders)} : elle ne peut pas être virée une seconde fois.",
                new { reason = "invoice-parcels-already-transferred", invoiceCode = invoice.Code, transferOrders = orders, parcelCodes = transferred.Take(20).Select(l => l.Code).ToList() });
        }
        var lines = TransferRules.LinesFor(invoices.Select(i => new TransferInvoice(i.Code, i.ClientId, i.Client,
            Money.ToCents(i.TotalDeliveryFee) + Money.ToCents(i.TotalFailureFee) + Money.ToCents(i.TotalAdjustment) + Money.ToCents(i.TotalVat), Money.ToCents(i.TotalDue), i.Lines.Count)));
        return lines.Select(l => (new TransferLineDto(l.ClientId, l.ClientName, l.ParcelCount, l.Collected, l.Fees, l.Due),
                invoices.Where(i => l.InvoiceCodes.Contains(i.Code)).SelectMany(i => i.Lines.Select(x => x.ParcelId)).ToList(), l.InvoiceCodes))
            .OrderBy(b => b.Item1.ClientName, StringComparer.Create(CultureInfo.InvariantCulture, false)).ToList();
    }

    /// <summary>
    /// Le lien OV → facture à l'émission : un OV « sélection » est tiré avant ses factures, il estampille ses colis mais ne
    /// peut pas nommer une facture qui n'existe pas encore. La facture s'inscrit ici dans tout OV qui a déjà viré un de ses colis.
    /// </summary>
    public static async Task LinkIssuedInvoiceAsync(IAppDbContext db, string invoiceCode, IReadOnlyList<string> parcelIds, CancellationToken cancellationToken)
    {
        if (parcelIds.Count == 0) return;
        var orders = await db.TransferOrders.Where(o => o.Parcels.Any(p => parcelIds.Contains(p.ParcelId)) && !o.InvoiceCodes.Contains(invoiceCode)).ToListAsync(cancellationToken);
        foreach (var order in orders) order.InvoiceCodes = [.. order.InvoiceCodes, invoiceCode];
        if (orders.Count > 0) await db.SaveChangesAsync(cancellationToken);
    }
}
