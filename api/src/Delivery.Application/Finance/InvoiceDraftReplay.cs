using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Finance;

/// <summary>
/// `InvoicesService.replayDraftLinesFor` : la part réglée par chèque d'un colis change (chèque rejeté ou annulé) ;
/// les lignes de factures BROUILLON qui le portent se recalculent. Les factures arrivent avec le module 9 :
/// sans facture brouillon, il n'y a rien à rejouer.
/// </summary>
public static class InvoiceDraftReplay
{
    public static async Task ChequeLinesAsync(IAppDbContext db, ICurrentUser caller, string parcelId, string authorName, string reason, CancellationToken cancellationToken)
    {
        var drafts = await db.InvoiceLines.AsNoTracking().AnyAsync(l => l.ParcelId == parcelId && l.InvoicedParcelId == null && l.Invoice.Status == InvoiceStatus.DRAFT, cancellationToken);
        if (!drafts) return;
        throw new InvalidOperationException($"Rejeu des lignes de facture brouillon ({caller.UserId}, {authorName}, {reason}) : branché avec le module 9.");
    }
}
