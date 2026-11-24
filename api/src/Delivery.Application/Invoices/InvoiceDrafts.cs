using System.Globalization;
using System.Text.Json;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Settings;
using Delivery.Application.Parcels;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Invoices;

/// <summary>
/// La facture brouillon d'un client et ses totaux (`InvoicesService` : `openDraftOf`,
/// `writeAdjustment`, `refreshTotals`, `chargePickupNote`). Les appelants ouvrent la transaction.
/// </summary>
public static class InvoiceDrafts
{
    public const string DefaultPrefix = "FA";

    public static async Task<(string Prefix, bool Yearly)> NumberingAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var prefix = await db.SettingAsync(SettingKeys.InvoicePrefix, cancellationToken) is { ValueKind: JsonValueKind.String } p ? p.GetString()! : DefaultPrefix;
        var yearly = await db.BoolSettingAsync(SettingKeys.NumberingYearlyReset, false, cancellationToken);
        return (prefix, yearly);
    }

    public static async Task<int> VatRateAsync(IAppDbContext db, CancellationToken cancellationToken) =>
        await db.SettingAsync(SettingKeys.VatRate, cancellationToken) is { ValueKind: JsonValueKind.Number } v && v.TryGetDouble(out var d) ? (int)d : 0;

    public static async Task<int> ProVatDefaultAsync(IAppDbContext db, CancellationToken cancellationToken) =>
        await db.SettingAsync(SettingKeys.ProVatRate, cancellationToken) is { ValueKind: JsonValueKind.Number } v && v.TryGetDouble(out var d) ? (int)d : Billing.DefaultProVatRate;

    /// <summary>`openDraftOf` : verrou consultatif par client, puis le brouillon le plus récent.</summary>
    public static async Task<Invoice?> OpenDraftOfAsync(IAppDbContext db, string clientId, CancellationToken cancellationToken)
    {
        var key = $"invoice-draft:{clientId}";
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))", cancellationToken);
        return await db.Invoices.Where(i => i.ClientId == clientId && i.Status == InvoiceStatus.DRAFT).OrderByDescending(i => i.CreatedAt).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>`writeAdjustment` : une ligne hors colis (frais), sa TVA, son journal, puis les totaux.</summary>
    public static async Task WriteAdjustmentAsync(IAppDbContext db, ICurrentUser caller, string invoiceId, InvoiceAdjustmentKind kind, long amountCents, string reason,
        string? reference, string? deliveryNoteId, string authorName, CancellationToken cancellationToken)
    {
        var draft = await db.Invoices.Where(i => i.Id == invoiceId && i.Status == InvoiceStatus.DRAFT).Select(i => new { i.Id, i.Client.IsPro }).FirstOrDefaultAsync(cancellationToken)
                    ?? throw BusinessException.Conflict("Cette facture est émise : elle ne se modifie plus.", new { reason = "invoice-issued" });
        var vatRate = draft.IsPro ? 0 : await VatRateAsync(db, cancellationToken);
        db.InvoiceAdjustments.Add(new InvoiceAdjustment
        {
            InvoiceId = invoiceId,
            Kind = kind,
            Amount = Money.FromCents(amountCents),
            VatAmount = Money.FromCents(InvoiceMath.SignedVat(amountCents, vatRate)),
            Reason = reason,
            Reference = reference,
            DeliveryNoteId = deliveryNoteId,
            CreatedById = caller.UserId,
            CreatedByName = authorName,
        });
        db.FieldChanges.Add(new FieldChange
        {
            Entity = "Invoice",
            EntityId = invoiceId,
            Field = "adjustment",
            OldValue = null,
            NewValue = $"{(amountCents / 100.0).ToString(CultureInfo.InvariantCulture)} MAD — {reason}",
            Reason = reason,
            AuthorId = caller.UserId,
            AuthorName = authorName,
            At = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
        await RefreshTotalsAsync(db, invoiceId, cancellationToken);
    }

    /// <summary>`refreshTotals` : lignes colis + lignes hors colis ; règle Pro (TVA sur le total HT) si le client est Pro.</summary>
    public static async Task RefreshTotalsAsync(IAppDbContext db, string invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await db.Invoices.Include(i => i.Client).FirstAsync(i => i.Id == invoiceId, cancellationToken);
        var lines = await db.InvoiceLines.Where(l => l.InvoiceId == invoiceId).ToListAsync(cancellationToken);
        var adjustments = await db.InvoiceAdjustments.Where(a => a.InvoiceId == invoiceId).ToListAsync(cancellationToken);
        int? proRate = invoice.Client.IsPro ? Billing.ProVatRateFor(true, invoice.Client.VatRate, await ProVatDefaultAsync(db, cancellationToken)) : null;
        InvoiceTotals totals;
        if (proRate is { } rate)
        {
            foreach (var line in lines)
            {
                var vat = Money.ToCents(line.VatAmount);
                if (vat == 0) continue;
                line.NetDue = Money.FromCents(Money.ToCents(line.NetDue) + vat);
                line.VatAmount = 0m;
            }
            foreach (var adjustment in adjustments.Where(a => Money.ToCents(a.VatAmount) != 0)) adjustment.VatAmount = 0m;
            totals = InvoiceMath.ProTotals(Sum(lines), adjustments.Select(a => Money.ToCents(a.Amount)), rate);
            invoice.ProVatRate = rate;
        }
        else
        {
            totals = InvoiceMath.Totals(Sum(lines), adjustments.Select(a => (Money.ToCents(a.Amount), Money.ToCents(a.VatAmount))));
            if (invoice.ProVatRate is not null) invoice.ProVatRate = null;
        }
        invoice.TotalCod = Money.FromCents(totals.Cod);
        invoice.TotalDeliveryFee = Money.FromCents(totals.DeliveryFee);
        invoice.TotalFailureFee = Money.FromCents(totals.FailureFee);
        invoice.TotalAdjustment = Money.FromCents(totals.Adjustment);
        invoice.TotalVat = Money.FromCents(totals.Vat);
        invoice.TotalDue = Money.FromCents(totals.Due);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static InvoiceLineSums Sum(IEnumerable<InvoiceLine> lines)
    {
        long cod = 0, delivery = 0, failure = 0, vat = 0, due = 0;
        foreach (var line in lines)
        {
            cod += Money.ToCents(line.CodAmount);
            delivery += Money.ToCents(line.DeliveryFee);
            failure += Money.ToCents(line.FailureFee);
            vat += Money.ToCents(line.VatAmount);
            due += Money.ToCents(line.NetDue);
        }
        return new InvoiceLineSums(cod, delivery, failure, vat, due);
    }

    /// <summary>`chargePickupNote` : les frais de ramassage d'un bon, une fois, sur le brouillon du client.</summary>
    public static async Task<string?> ChargePickupNoteAsync(IAppDbContext db, ICurrentUser caller, string noteId, string noteCode, string clientId, string agencyId,
        int parcelCount, long amountCents, DateTime now, CancellationToken cancellationToken)
    {
        if (amountCents <= 0) return null;
        if (await db.InvoiceAdjustments.AnyAsync(a => a.DeliveryNoteId == noteId, cancellationToken)) return null;
        var client = await db.Clients.AsNoTracking().Where(c => c.Id == clientId).Select(c => new { c.Id, c.AgencyId, c.BillingType }).FirstOrDefaultAsync(cancellationToken);
        if (client is null) return null;
        var invoice = await OpenDraftOfAsync(db, client.Id, cancellationToken);
        if (invoice is null)
        {
            var (prefix, yearly) = await NumberingAsync(db, cancellationToken);
            var code = await DocumentCodes.ReserveAsync(db, prefix, now, cancellationToken, yearly);
            invoice = new Invoice { Code = code, ClientId = client.Id, AgencyId = client.AgencyId ?? agencyId, BillingType = client.BillingType, CreatedById = caller.UserId, CreatedAt = now };
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync(cancellationToken);
        }
        var author = await Parcels.Commands.ParcelStatusChange.AuthorNameAsync(db, caller.UserId, cancellationToken);
        await WriteAdjustmentAsync(db, caller, invoice.Id, InvoiceAdjustmentKind.PICKUP_NOTE_FEE, amountCents,
            $"Frais de ramassage — bon {noteCode} ({parcelCount} colis ramassés)", noteCode, noteId, author, cancellationToken);
        return invoice.Code;
    }
}
