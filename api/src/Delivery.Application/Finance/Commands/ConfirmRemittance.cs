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

/// <summary>
/// `POST /remittances/:code/confirm` : l'agence reçoit la caisse. Lignes figées, colis « remis par le livreur », chèques
/// reçus par la société, attendus recalculés et écart porté au solde du livreur, le tout en une transaction.
/// </summary>
public sealed record ConfirmRemittanceCommand(string Code) : IRequest<RemittanceDetailDto>;

internal sealed class ConfirmRemittanceHandler(IAppDbContext db, ICurrentUser caller, ILogger<ConfirmRemittanceHandler> logger) : IRequestHandler<ConfirmRemittanceCommand, RemittanceDetailDto>
{
    public async Task<RemittanceDetailDto> Handle(ConfirmRemittanceCommand request, CancellationToken cancellationToken)
    {
        if (caller.CourierId is not null) throw BusinessException.Forbidden("Seule l’agence clôture un retour de fonds.", new { reason = "closing-is-agency-only" });
        var remittance = await Remittances.Scoped(db.CashRemittances.AsNoTracking(), caller).Where(r => r.Code == request.Code)
                             .Select(r => new { r.Id, r.Code, r.Status, r.AgencyId, r.CourierId, Lines = r.Parcels.Count }).FirstOrDefaultAsync(cancellationToken)
                         ?? throw BusinessException.NotFound($"Retour de fonds introuvable : {request.Code}");
        if (remittance.Status == CashRemittanceStatus.CONFIRMED) throw BusinessException.Conflict("Ce retour de fonds est déjà confirmé.", new { reason = "remittance-already-confirmed" });
        if (remittance.Status == CashRemittanceStatus.CANCELLED) throw BusinessException.Conflict("Ce retour de fonds est annulé.", new { reason = "remittance-cancelled" });
        if (remittance.Lines == 0) throw BusinessException.Validation("Un retour de fonds sans colis ne se confirme pas.");
        await ReturnRows.AssertAgencyReachableAsync(db, caller, remittance.AgencyId, cancellationToken);
        var now = DateTime.UtcNow;
        var authorName = await Remittances.AuthorNameAsync(db, caller.UserId, cancellationToken);
        long? declared = null;
        long net = 0;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var claimed = await db.CashRemittances.Where(r => r.Id == remittance.Id && r.Status == CashRemittanceStatus.DRAFT)
                .ExecuteUpdateAsync(set => set.SetProperty(r => r.Status, CashRemittanceStatus.CONFIRMED).SetProperty(r => r.ConfirmedAt, now).SetProperty(r => r.ConfirmedById, caller.UserId),
                    cancellationToken);
            if (claimed == 0) throw BusinessException.Conflict("Ce retour de fonds vient d’être confirmé.", new { reason = "remittance-already-confirmed" });
            var fresh = await db.CashRemittances.AsNoTracking().Where(r => r.Id == remittance.Id).Select(r => new { r.DeclaredAmount }).FirstAsync(cancellationToken);
            var lines = await db.CashRemittanceParcels.AsNoTracking().Where(l => l.RemittanceId == remittance.Id).Select(l => new { l.ParcelId, l.Amount, l.Cheque, l.Commission })
                .ToListAsync(cancellationToken);
            var lineIds = lines.Select(l => l.ParcelId).ToList();
            var notDelivered = await db.Parcels.AsNoTracking().Where(p => lineIds.Contains(p.Id) && p.StatusId != Billing.DeliveredStatusId).Select(p => p.Code).ToListAsync(cancellationToken);
            if (notDelivered.Count > 0)
            {
                throw BusinessException.Conflict($"{notDelivered.Count} colis de ce bon ne sont plus « Livré » : retirez-les avant de confirmer.",
                    new { reason = "remittance-parcel-not-delivered", codes = notDelivered });
            }
            var totals = Remittances.Sum(lines.Select(l => (l.Amount, l.Cheque, l.Commission)));
            net = totals.Net;
            declared = Money.ToCentsOrNull(fresh.DeclaredAmount);
            var variance = declared is null ? 0 : declared.Value - net;
            foreach (var parcelId in lineIds)
            {
                await db.CashRemittanceParcels.Where(l => l.RemittanceId == remittance.Id && l.ParcelId == parcelId)
                    .ExecuteUpdateAsync(set => set.SetProperty(l => l.ConfirmedParcelId, parcelId), cancellationToken);
            }
            await db.Parcels.Where(p => lineIds.Contains(p.Id)).ExecuteUpdateAsync(set => set.SetProperty(p => p.ReimbursedByCourier, true).SetProperty(p => p.ReimbursedAt, now)
                .SetProperty(p => p.ReimbursedRemittanceId, remittance.Id), cancellationToken);
            await db.Parcels.Where(p => lineIds.Contains(p.Id) && (p.FinancialStatus == FinancialStatus.NON_ENCAISSE || p.FinancialStatus == FinancialStatus.ENCAISSE))
                .ExecuteUpdateAsync(set => set.SetProperty(p => p.FinancialStatus, FinancialStatus.REMIS_LIVREUR), cancellationToken);
            await ReceiveChequesAsync(remittance.Code, lineIds, now, authorName, cancellationToken);
            await db.CashRemittances.Where(r => r.Id == remittance.Id).ExecuteUpdateAsync(set => set
                .SetProperty(r => r.ExpectedCod, Money.FromCents(totals.Cod)).SetProperty(r => r.ExpectedCommission, Money.FromCents(totals.Commission))
                .SetProperty(r => r.ExpectedNet, Money.FromCents(totals.Net)).SetProperty(r => r.TotalCash, Money.FromCents(totals.Cash)).SetProperty(r => r.TotalCheque, Money.FromCents(totals.Cheque))
                .SetProperty(r => r.Variance, Money.FromCents(variance)).SetProperty(r => r.Amount, Money.FromCents(declared ?? totals.Net)).SetProperty(r => r.UpdatedAt, now), cancellationToken);
            if (variance != 0)
            {
                db.CourierBalanceEntries.Add(new CourierBalanceEntry
                {
                    CourierId = remittance.CourierId, Kind = CourierBalanceEntryKind.VARIANCE, Amount = Money.FromCents(variance), RemittanceId = remittance.Id, AuthorId = caller.UserId,
                    AuthorName = authorName, At = now,
                });
                await db.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            throw BusinessException.Conflict("Le cash d’un colis de cette recette vient d’être remis ailleurs. Retirez-le et reconfirmez.", new { reason = "parcel-already-remitted" });
        }
        catch (Npgsql.PostgresException error) when (error.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation)
        {
            throw BusinessException.Conflict("Le cash d’un colis de cette recette vient d’être remis ailleurs. Retirez-le et reconfirmez.", new { reason = "parcel-already-remitted" });
        }
        await Notify(() => Notifier.NotifyCourierAsync(db, remittance.CourierId, "remittance-confirmed", new Dictionary<string, object> { ["code"] = remittance.Code },
            new Dictionary<string, string> { ["remittanceCode"] = remittance.Code }, cancellationToken));
        if (declared is not null && declared != net)
        {
            await Notify(() => Notifier.NotifyStaffAsync(db, remittance.AgencyId, "cash-variance", "finance.read", new Dictionary<string, object> { ["code"] = remittance.Code },
                new Dictionary<string, string> { ["remittanceCode"] = remittance.Code }, cancellationToken));
        }
        return await Remittances.DetailAsync(db, caller, request.Code, cancellationToken);
    }

    private async Task Notify(Func<Task> send)
    {
        try
        {
            await send();
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("Notification de caisse non envoyée : {Message}", error.Message);
        }
    }

    /// <summary>`receiveCheques` : les chèques des colis passent « reçus par la société ».</summary>
    private async Task ReceiveChequesAsync(string remittanceCode, IReadOnlyList<string> parcelIds, DateTime now, string authorName, CancellationToken cancellationToken)
    {
        var waiting = await db.Cheques.AsNoTracking().Where(c => parcelIds.Contains(c.ParcelId) && (c.Status == ChequeStatus.RECU_LIVREUR || c.Status == ChequeStatus.REMIS_ADMIN))
            .Select(c => new { c.Id, c.Status }).ToListAsync(cancellationToken);
        if (waiting.Count == 0) return;
        var comment = $"Reçu par {await ReturnRows.ShortNameAsync(db, cancellationToken)} à la réception de {remittanceCode}";
        foreach (var cheque in waiting)
        {
            var fromCourier = cheque.Status == ChequeStatus.RECU_LIVREUR;
            var moved = await db.Cheques.Where(c => c.Id == cheque.Id && (c.Status == ChequeStatus.RECU_LIVREUR || c.Status == ChequeStatus.REMIS_ADMIN))
                .ExecuteUpdateAsync(set => set.SetProperty(c => c.Status, ChequeStatus.RECU_ADMIN).SetProperty(c => c.ReceivedByAdminAt, now)
                    .SetProperty(c => c.HandedOverAt, c => fromCourier ? now : c.HandedOverAt).SetProperty(c => c.UpdatedAt, now), cancellationToken);
            if (moved == 0) continue;
            db.ChequeEvents.Add(new ChequeEvent { ChequeId = cheque.Id, Status = ChequeStatus.RECU_ADMIN, Comment = comment, AuthorId = caller.UserId, AuthorName = authorName, CreatedAt = now });
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
