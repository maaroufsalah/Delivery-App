using System.Globalization;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Invoices;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Finance;

/// <summary>
/// `InvoicesService.replayDraftLinesFor` : la part réglée par chèque d'un colis change (chèque rejeté ou annulé) ;
/// chaque ligne de facture BROUILLON qui le porte se recalcule, le changement est tracé, puis les totaux.
/// </summary>
public static class InvoiceDraftReplay
{
    public static async Task ChequeLinesAsync(IAppDbContext db, ICurrentUser caller, string parcelId, string authorName, string reason, CancellationToken cancellationToken)
    {
        var lines = await db.InvoiceLines.AsNoTracking().Where(l => l.ParcelId == parcelId && l.InvoicedParcelId == null && l.Invoice.Status == InvoiceStatus.DRAFT)
            .Select(l => new { l.Id, l.InvoiceId, l.ChequePaid, l.Invoice.BillingType, l.Invoice.Client.IsPro }).ToListAsync(cancellationToken);
        if (lines.Count == 0) return;
        var vatRate = await InvoiceDrafts.VatRateAsync(db, cancellationToken);
        var parcel = await InvoiceRows.MoneyOf(db.Parcels.AsNoTracking().Where(p => p.Id == parcelId)).FirstOrDefaultAsync(cancellationToken);
        if (parcel is null) return;
        var chequePaid = parcel.ChequePaid;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var line in lines)
        {
            var before = Money.ToCents(line.ChequePaid);
            if (before == chequePaid) continue;
            var amounts = InvoiceRows.LineAmounts(parcel.Billable, line.IsPro ? 0 : vatRate, line.BillingType, chequePaid);
            await db.InvoiceLines.Where(l => l.Id == line.Id).ExecuteUpdateAsync(s => s.SetProperty(l => l.ChequePaid, Money.FromCents(amounts.ChequePaid))
                .SetProperty(l => l.NetDue, Money.FromCents(amounts.NetDue)), cancellationToken);
            db.FieldChanges.Add(new FieldChange
            {
                Entity = "Parcel", EntityId = parcelId, Field = "chequePaid", OldValue = (before / 100.0).ToString(CultureInfo.InvariantCulture),
                NewValue = (chequePaid / 100.0).ToString(CultureInfo.InvariantCulture), Reason = reason, AuthorId = caller.UserId, AuthorName = authorName, At = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
            await InvoiceDrafts.RefreshTotalsAsync(db, line.InvoiceId, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }
}
