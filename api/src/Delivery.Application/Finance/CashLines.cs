using System.Text.Json;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Settings;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Finance;

/// <summary>Ce qu'une livraison verse à la recette du livreur (`attachDelivered`), en centimes.</summary>
public sealed record DeliveredCash(string CourierId, string AgencyId, string CityId, string ParcelId, long Cash, long Cheque, long Commission, SettlementMode SettlementMode, PayModel PayModel);

/// <summary>Les bons de recette brouillons du livreur (`RemittancesService` : `attachDelivered`, `detachDelivered`, `refreshTotal`). L'appelant ouvre la transaction.</summary>
public static class RemittanceLines
{
    public const string DefaultPrefix = "RF";

    /// <summary>
    /// La livraison rejoint le bon où le livreur l'a déjà comptée (bon clos par lui), sinon son bon ouvert, sinon un
    /// bon automatique neuf. Un verrou consultatif par livreur remplace le SAVEPOINT de Speedy : deux livraisons
    /// simultanées n'ouvrent jamais deux bons.
    /// </summary>
    public static async Task<string> AttachDeliveredAsync(IAppDbContext db, DeliveredCash input, CancellationToken cancellationToken)
    {
        var held = await db.CashRemittanceParcels
            .Where(l => l.ParcelId == input.ParcelId && l.ConfirmedParcelId == null && l.Remittance.CourierId == input.CourierId && l.Remittance.Status == CashRemittanceStatus.DRAFT
                        && l.Remittance.CourierClosedAt != null)
            .Select(l => l.RemittanceId).FirstOrDefaultAsync(cancellationToken);
        if (held is not null)
        {
            var taken = await db.CashRemittances.Where(r => r.Id == held && r.Status == CashRemittanceStatus.DRAFT)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.DeclaredAmount, (decimal?)null), cancellationToken);
            if (taken == 0) return held;
            await WriteLineAsync(db, held, input, cancellationToken);
            return held;
        }

        var lockKey = $"remittance-draft:{input.CourierId}";
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext({lockKey}))", cancellationToken);
        var remittanceId = await db.CashRemittances.Where(r => r.CourierId == input.CourierId && r.Status == CashRemittanceStatus.DRAFT && r.CourierClosedAt == null)
            .Select(r => r.Id).FirstOrDefaultAsync(cancellationToken);
        if (remittanceId is not null)
        {
            var taken = await db.CashRemittances.Where(r => r.Id == remittanceId && r.Status == CashRemittanceStatus.DRAFT && r.CourierClosedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.DeclaredAmount, (decimal?)null), cancellationToken);
            if (taken == 0) remittanceId = null;
        }
        if (remittanceId is null)
        {
            var prefix = await db.SettingAsync(SettingKeys.RemittancePrefix, cancellationToken) is { ValueKind: JsonValueKind.String } p ? p.GetString()! : DefaultPrefix;
            var yearly = await db.BoolSettingAsync(SettingKeys.NumberingYearlyReset, false, cancellationToken);
            var code = await DocumentCodes.ReserveAsync(db, prefix, DateTime.UtcNow, cancellationToken, yearly);
            var created = new CashRemittance
            {
                Code = code, Status = CashRemittanceStatus.DRAFT, Origin = RemittanceOrigin.AUTOMATIC, CityId = input.CityId, CourierId = input.CourierId, AgencyId = input.AgencyId,
                SettlementMode = input.SettlementMode, PayModel = input.PayModel,
            };
            db.CashRemittances.Add(created);
            await db.SaveChangesAsync(cancellationToken);
            remittanceId = created.Id;
        }
        await WriteLineAsync(db, remittanceId, input, cancellationToken);
        return remittanceId;
    }

    /// <summary>`detachDelivered` : la livraison quitte les bons brouillons qui la portent ; rend leurs codes.</summary>
    public static async Task<IReadOnlyList<string>> DetachDeliveredAsync(IAppDbContext db, string parcelId, CancellationToken cancellationToken)
    {
        var lines = await db.CashRemittanceParcels.Where(l => l.ParcelId == parcelId && l.ConfirmedParcelId == null && l.Remittance.Status == CashRemittanceStatus.DRAFT)
            .Select(l => new { l.RemittanceId, l.Remittance.Code }).ToListAsync(cancellationToken);
        foreach (var line in lines)
        {
            await db.CashRemittanceParcels.Where(l => l.RemittanceId == line.RemittanceId && l.ParcelId == parcelId).ExecuteDeleteAsync(cancellationToken);
            await RefreshTotalAsync(db, line.RemittanceId, cancellationToken);
        }
        return lines.Select(l => l.Code).ToList();
    }

    /// <summary>
    /// `refreshTotal` : les montants attendus suivent les lignes ; les espèces se déduisent (COD − chèques), et un
    /// comptage saisi avant le changement est effacé (CALC-05).
    /// </summary>
    public static async Task RefreshTotalAsync(IAppDbContext db, string remittanceId, CancellationToken cancellationToken)
    {
        var lines = await db.CashRemittanceParcels.AsNoTracking().Where(l => l.RemittanceId == remittanceId).Select(l => new { l.Amount, l.Cheque, l.Commission }).ToListAsync(cancellationToken);
        var cod = lines.Sum(l => Money.ToCents(l.Amount));
        var cheque = lines.Sum(l => Money.ToCents(l.Cheque));
        var commission = lines.Sum(l => Money.ToCents(l.Commission));
        var cash = cod - cheque;
        var net = cash - commission;
        await db.CashRemittances.Where(r => r.Id == remittanceId).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.ExpectedCod, Money.FromCents(cod))
            .SetProperty(r => r.ExpectedCommission, Money.FromCents(commission))
            .SetProperty(r => r.ExpectedNet, Money.FromCents(net))
            .SetProperty(r => r.TotalCash, Money.FromCents(cash))
            .SetProperty(r => r.TotalCheque, Money.FromCents(cheque))
            .SetProperty(r => r.Amount, Money.FromCents(net))
            .SetProperty(r => r.DeclaredAmount, (decimal?)null)
            .SetProperty(r => r.VarianceReason, (string?)null), cancellationToken);
    }

    /// <summary>`writeLine` : la commission n'est retenue sur le bon qu'en règlement à la recette.</summary>
    private static async Task WriteLineAsync(IAppDbContext db, string remittanceId, DeliveredCash input, CancellationToken cancellationToken)
    {
        var deducted = input.SettlementMode == SettlementMode.AT_REMITTANCE ? input.Commission : 0;
        var collected = input.Cash + input.Cheque;
        var line = await db.CashRemittanceParcels.FirstOrDefaultAsync(l => l.RemittanceId == remittanceId && l.ParcelId == input.ParcelId, cancellationToken);
        if (line is null)
        {
            line = new CashRemittanceParcel { RemittanceId = remittanceId, ParcelId = input.ParcelId };
            db.CashRemittanceParcels.Add(line);
        }
        line.Amount = Money.FromCents(collected);
        line.Cash = Money.FromCents(input.Cash);
        line.Cheque = Money.FromCents(input.Cheque);
        line.Commission = Money.FromCents(deducted);
        await db.SaveChangesAsync(cancellationToken);
        await RefreshTotalAsync(db, remittanceId, cancellationToken);
    }
}

/// <summary>Le chèque reçu à la livraison (`ChequesService.attach`).</summary>
public sealed record ChequeIntake(string ParcelId, string CourierId, string? RemittanceId, string PayerName, long Amount, string Number, string? Bank, DateOnly? ChequeDate, string AuthorName);

public static class ChequeIntakes
{
    /// <summary>Un chèque déjà parti du livreur ne se réécrit plus ; sinon il est créé ou mis à jour, puis les versements suivent.</summary>
    public static async Task AttachAsync(IAppDbContext db, ICurrentUser caller, ChequeIntake input, CancellationToken cancellationToken)
    {
        var existing = await db.Cheques.FirstOrDefaultAsync(c => c.ParcelId == input.ParcelId, cancellationToken);
        if (existing is not null && existing.Status != ChequeStatus.RECU_LIVREUR) return;
        if (existing is null)
        {
            existing = new Cheque { ParcelId = input.ParcelId, Status = ChequeStatus.RECU_LIVREUR, ReceivedAt = DateTime.UtcNow };
            db.Cheques.Add(existing);
            db.ChequeEvents.Add(new ChequeEvent
            {
                ChequeId = existing.Id, Status = ChequeStatus.RECU_LIVREUR, Kind = "transition", Comment = "Chèque reçu à la livraison", AuthorId = caller.UserId, AuthorName = input.AuthorName,
                CreatedAt = DateTime.UtcNow,
            });
        }
        existing.Number = input.Number;
        existing.Amount = Money.FromCents(input.Amount);
        existing.Bank = input.Bank;
        existing.ChequeDate = input.ChequeDate;
        existing.PayerName = input.PayerName;
        existing.CourierId = input.CourierId;
        existing.RemittanceId = input.RemittanceId;
        await db.SaveChangesAsync(cancellationToken);
        await CodPayoutDrafts.RefreshDraftsForAsync(db, input.ParcelId, cancellationToken);
    }
}

/// <summary>Les bons de versement brouillons d'un colis (`CodPayoutsService.refreshDraftsFor`).</summary>
public static class CodPayoutDrafts
{
    /// <summary>Le montant versable suit le chèque : une ligne à zéro sort du bon.</summary>
    public static async Task RefreshDraftsForAsync(IAppDbContext db, string parcelId, CancellationToken cancellationToken)
    {
        var links = await db.CodPayoutParcels.Where(l => l.ParcelId == parcelId && l.PaidOutParcelId == null && l.Payout.Status == CodPayoutStatus.DRAFT)
            .Select(l => new
            {
                l.PayoutId, l.Amount, l.Parcel.CodAmount, l.Parcel.DeliveryFee, l.Parcel.FeeIncluded, l.Parcel.AlreadyPaid,
                Cheque = l.Parcel.Cheque == null ? null : new { l.Parcel.Cheque.Amount, l.Parcel.Cheque.Status },
                Remitted = l.Parcel.Remittances.Where(r => r.Remittance.Status != CashRemittanceStatus.CANCELLED).Select(r => new { r.Cheque, r.ConfirmedParcelId }).ToList(),
            }).ToListAsync(cancellationToken);
        foreach (var link in links)
        {
            var collected = Billing.CodToCollect(Money.ToCents(link.CodAmount), Money.ToCents(link.DeliveryFee), link.FeeIncluded, link.AlreadyPaid);
            var remitted = link.Remitted.FirstOrDefault(r => r.ConfirmedParcelId != null) ?? link.Remitted.FirstOrDefault();
            var paid = CourierPay.ChequePaidOn(link.Cheque is null ? null : Money.ToCents(link.Cheque.Amount), link.Cheque?.Status == ChequeStatus.ANNULE,
                remitted is null ? null : Money.ToCents(remitted.Cheque));
            var amount = Billing.CodPayoutAmount(collected, paid);
            if (amount == Money.ToCents(link.Amount)) continue;
            var row = db.CodPayoutParcels.Where(l => l.PayoutId == link.PayoutId && l.ParcelId == parcelId);
            if (amount == 0) await row.ExecuteDeleteAsync(cancellationToken);
            else await row.ExecuteUpdateAsync(s => s.SetProperty(l => l.Amount, Money.FromCents(amount)), cancellationToken);
            var total = (await db.CodPayoutParcels.AsNoTracking().Where(l => l.PayoutId == link.PayoutId).Select(l => l.Amount).ToListAsync(cancellationToken)).Sum(a => Money.ToCents(a));
            await db.CodPayouts.Where(p => p.Id == link.PayoutId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Amount, Money.FromCents(total)), cancellationToken);
        }
    }
}
