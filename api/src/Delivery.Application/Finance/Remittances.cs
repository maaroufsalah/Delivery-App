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

/// <summary>`RemittancesService` : portée, étapes affichées, projections et détail.</summary>
public static class Remittances
{
    public static readonly string[] Stages = ["open", "closed", "received", "validated", "cancelled"];
    public static readonly string[] Statuses = ["draft", "confirmed", "cancelled"];

    /// <summary>`remittanceWhere` : administrateur tout, livreur les siennes, vendeur rien, agent son agence.</summary>
    public static IQueryable<CashRemittance> Scoped(IQueryable<CashRemittance> remittances, ICurrentUser caller)
    {
        if (caller.IsSuperAdmin()) return remittances;
        if (caller.CourierId is { } courierId) return remittances.Where(r => r.CourierId == courierId);
        if (caller.ClientId is not null) return remittances.Where(r => r.Id == Scope.DenyAllId);
        if (caller.AgencyId is { } agencyId) return remittances.Where(r => r.AgencyId == agencyId);
        return remittances.Where(r => r.Id == Scope.DenyAllId);
    }

    /// <summary>`stageWhere`, en OU des étapes demandées. « Reçue » tant qu'une part d'espèces attend son vendeur, « validée » ensuite.</summary>
    public static IQueryable<CashRemittance> InStages(IQueryable<CashRemittance> where, IReadOnlyCollection<string> stages)
    {
        bool open = stages.Contains("open"), closed = stages.Contains("closed"), received = stages.Contains("received"), validated = stages.Contains("validated"),
            cancelled = stages.Contains("cancelled");
        return where.Where(r =>
            (open && r.Status == CashRemittanceStatus.DRAFT && r.CourierClosedAt == null)
            || (closed && r.Status == CashRemittanceStatus.DRAFT && r.CourierClosedAt != null)
            || (received && r.Status == CashRemittanceStatus.CONFIRMED && r.Parcels.Any(l => l.Cash > 0 && !(l.Parcel.FinancialStatus == FinancialStatus.REMBOURSE
                || l.Parcel.CodPayouts.Any(c => c.Payout.Status == CodPayoutStatus.PAID))))
            || (validated && r.Status == CashRemittanceStatus.CONFIRMED && r.Parcels.Any() && !r.Parcels.Any(l => l.Cash > 0 && !(l.Parcel.FinancialStatus == FinancialStatus.REMBOURSE
                || l.Parcel.CodPayouts.Any(c => c.Payout.Status == CodPayoutStatus.PAID))))
            || (cancelled && r.Status == CashRemittanceStatus.CANCELLED));
    }

    public static string StageOf(CashRemittanceStatus status, DateTime? courierClosedAt, bool validated) => status switch
    {
        CashRemittanceStatus.CANCELLED => "cancelled",
        CashRemittanceStatus.CONFIRMED => validated ? "validated" : "received",
        _ => courierClosedAt is null ? "open" : "closed",
    };

    private sealed record Row(string Id, string Code, CashRemittanceStatus Status, string CourierId, string CityId, string AgencyId, decimal Amount, decimal ExpectedCod, decimal ExpectedCommission,
        decimal ExpectedNet, decimal TotalCash, decimal TotalCheque, decimal? DeclaredAmount, string? VarianceReason, DateTime? ConfirmedAt, DateTime? CourierClosedAt, DateTime CreatedAt,
        string CourierName, SettlementMode SettlementMode, string CityName, string AgencyName, string? ConfirmedByName, string? CourierClosedByName, int ParcelCount, int ChequeCount, bool Validated);

    private static IQueryable<Row> Rows(IQueryable<CashRemittance> remittances) => remittances.Select(r => new Row(r.Id, r.Code, r.Status, r.CourierId, r.CityId, r.AgencyId, r.Amount,
        r.ExpectedCod, r.ExpectedCommission, r.ExpectedNet, r.TotalCash, r.TotalCheque, r.DeclaredAmount, r.VarianceReason, r.ConfirmedAt, r.CourierClosedAt, r.CreatedAt,
        r.Courier.FirstName + " " + r.Courier.LastName, r.Courier.SettlementMode, r.City.Name, r.Agency.Name,
        r.ConfirmedBy != null ? r.ConfirmedBy.FirstName + " " + r.ConfirmedBy.LastName : null, r.CourierClosedBy != null ? r.CourierClosedBy.FirstName + " " + r.CourierClosedBy.LastName : null,
        r.Parcels.Count, r.Cheques.Count,
        r.Status == CashRemittanceStatus.CONFIRMED && r.Parcels.Any() && !r.Parcels.Any(l => l.Cash > 0 && !(l.Parcel.FinancialStatus == FinancialStatus.REMBOURSE
            || l.Parcel.CodPayouts.Any(c => c.Payout.Status == CodPayoutStatus.PAID)))));

    private static T Fill<T>(T item, Row row) where T : RemittanceListItemDto
    {
        var expectedNet = Money.ToCents(row.ExpectedNet);
        var declared = Money.ToCentsOrNull(row.DeclaredAmount);
        item.Id = row.Id; item.Code = row.Code; item.Status = Money.ToWire(row.Status); item.Stage = StageOf(row.Status, row.CourierClosedAt, row.Validated);
        item.CourierClosedAt = row.CourierClosedAt; item.CourierClosedByName = row.CourierClosedByName; item.CourierId = row.CourierId; item.CourierName = row.CourierName;
        item.CityId = row.CityId; item.CityName = row.CityName; item.AgencyId = row.AgencyId; item.AgencyName = row.AgencyName; item.ParcelCount = row.ParcelCount;
        item.Amount = Money.ToCents(row.Amount); item.TotalCash = Money.ToCents(row.TotalCash); item.TotalCheque = Money.ToCents(row.TotalCheque); item.ChequeCount = row.ChequeCount;
        item.ExpectedCod = Money.ToCents(row.ExpectedCod); item.ExpectedCommission = Money.ToCents(row.ExpectedCommission); item.ExpectedNet = expectedNet; item.DeclaredAmount = declared;
        item.Variance = declared is null ? 0 : declared.Value - expectedNet; item.VarianceReason = row.VarianceReason; item.SettlementMode = Money.ToWire(row.SettlementMode);
        item.ConfirmedAt = row.ConfirmedAt; item.ConfirmedByName = row.ConfirmedByName; item.CreatedAt = row.CreatedAt;
        return item;
    }

    public static async Task<List<RemittanceListItemDto>> ListAsync(IQueryable<CashRemittance> remittances, CancellationToken cancellationToken) =>
        (await Rows(remittances).ToListAsync(cancellationToken)).Select(r => Fill(new RemittanceListItemDto(), r)).ToList();

    private static RemittanceChequeDto Cheque(string id, string number, string? bank, decimal amount, ChequeStatus status, string parcelId, string parcelCode) =>
        new(id, number, bank, Money.ToCents(amount), Money.ToWire(status), parcelId, parcelCode);

    public static async Task<RemittanceDetailDto> DetailAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken)
    {
        var scoped = Scoped(db.CashRemittances.AsNoTracking(), caller).Where(r => r.Code == code);
        var row = await Rows(scoped).FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound($"Retour de fonds introuvable : {code}");
        var note = await scoped.Select(r => r.Note).FirstAsync(cancellationToken);
        var lines = await db.CashRemittanceParcels.AsNoTracking().Where(l => l.RemittanceId == row.Id)
            .Select(l => new
            {
                l.ParcelId, l.Parcel.Code, Client = l.Parcel.Client.Name, l.Parcel.RecipientName, l.Amount, l.Cash, l.Cheque, l.Commission,
                ParcelCheque = l.Parcel.Cheque == null ? null : new { l.Parcel.Cheque.Id, l.Parcel.Cheque.Number, l.Parcel.Cheque.Bank, l.Parcel.Cheque.Amount, l.Parcel.Cheque.Status },
            }).ToListAsync(cancellationToken);
        var cheques = await db.Cheques.AsNoTracking().Where(c => c.RemittanceId == row.Id).OrderBy(c => c.ReceivedAt)
            .Select(c => new { c.Id, c.Number, c.Bank, c.Amount, c.Status, c.ParcelId, ParcelCode = c.Parcel.Code }).ToListAsync(cancellationToken);
        var closed = row.Status == CashRemittanceStatus.DRAFT && row.CourierClosedAt is not null;
        var detail = Fill(new RemittanceDetailDto(), row);
        detail.Note = note;
        detail.Confirmable = row.Status == CashRemittanceStatus.DRAFT && lines.Count > 0;
        detail.Reopenable = closed && await OtherOpenCodeAsync(db, row.CourierId, cancellationToken) is null;
        detail.Items = lines.Select(l => new RemittanceItemDto(l.ParcelId, l.Code, l.Client, l.RecipientName, Money.ToCents(l.Amount), Money.ToCents(l.Cash), Money.ToCents(l.Cheque),
            l.ParcelCheque is { } c ? [Cheque(c.Id, c.Number, c.Bank, c.Amount, c.Status, l.ParcelId, l.Code)] : [], Money.ToCents(l.Commission))).ToList();
        detail.Cheques = cheques.Select(c => Cheque(c.Id, c.Number, c.Bank, c.Amount, c.Status, c.ParcelId, c.ParcelCode)).ToList();
        return detail;
    }

    public static Task<string?> OtherOpenCodeAsync(IAppDbContext db, string courierId, CancellationToken cancellationToken) =>
        db.CashRemittances.AsNoTracking().Where(r => r.CourierId == courierId && r.Status == CashRemittanceStatus.DRAFT && r.CourierClosedAt == null).Select(r => r.Code)
            .FirstOrDefaultAsync(cancellationToken);

    public static BusinessException HasNewerOpen(string code) => BusinessException.Conflict(
        $"Ce livreur a déjà une nouvelle caisse ouverte{(code.Length > 0 ? $" ({code})" : string.Empty)} : celle-ci ne se rouvre plus. Corrigez directement ses lignes.",
        new { reason = "courier-has-newer-remittance", code });

    public sealed record Draft(string Id, string CourierId, string AgencyId, SettlementMode SettlementMode);

    /// <summary>`requireDraft` : le règlement des commissions est celui du livreur, pas celui figé sur le bon.</summary>
    public static async Task<Draft> RequireDraftAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken)
    {
        var remittance = await Scoped(db.CashRemittances.AsNoTracking(), caller).Where(r => r.Code == code)
                             .Select(r => new { r.Id, r.Status, r.CourierId, r.AgencyId, r.Courier.SettlementMode }).FirstOrDefaultAsync(cancellationToken)
                         ?? throw BusinessException.NotFound($"Retour de fonds introuvable : {code}");
        if (remittance.Status != CashRemittanceStatus.DRAFT) throw BusinessException.Conflict("Ce retour de fonds est confirmé : il ne se modifie plus.", new { reason = "remittance-confirmed" });
        return new Draft(remittance.Id, remittance.CourierId, remittance.AgencyId, remittance.SettlementMode);
    }

    public static (long Cod, long Cash, long Cheque, long Commission, long Net) Sum(IEnumerable<(decimal Amount, decimal Cheque, decimal Commission)> lines)
    {
        long cod = 0, cheque = 0, commission = 0;
        foreach (var line in lines)
        {
            cod += Money.ToCents(line.Amount);
            cheque += Money.ToCents(line.Cheque);
            commission += Money.ToCents(line.Commission);
        }
        var cash = cod - cheque;
        return (cod, cash, cheque, commission, cash - commission);
    }

    public static long Collected(decimal codAmount, decimal deliveryFee, bool feeIncluded, bool alreadyPaid) =>
        Billing.CodToCollect(Money.ToCents(codAmount), Money.ToCents(deliveryFee), feeIncluded, alreadyPaid);

    public static async Task<string> AuthorNameAsync(IAppDbContext db, string? userId, CancellationToken cancellationToken)
    {
        if (userId is null) return "Système";
        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.FirstName, u.LastName }).FirstOrDefaultAsync(cancellationToken);
        return user is null ? "Système" : $"{user.FirstName} {user.LastName}";
    }

    /// <summary>`addParcels` : chaque colis livré, encaissé, de ce livreur et pas encore remis ; ligne écrite ou réécrite.</summary>
    public static async Task AddParcelsAsync(IAppDbContext db, ICurrentUser caller, string code, IReadOnlyList<string> parcelIds, CancellationToken cancellationToken)
    {
        var remittance = await RequireDraftAsync(db, caller, code, cancellationToken);
        var parcels = await db.Parcels.AsNoTracking().Where(p => parcelIds.Contains(p.Id) && p.DeletedAt == null)
            .Select(p => new
            {
                p.Id, p.Code, p.StatusId, p.FinancialStatus, p.CourierId, p.CodAmount, p.DeliveryFee, p.FeeIncluded, p.AlreadyPaid, p.CourierCommission,
                Remitted = p.Remittances.Any(l => l.ConfirmedParcelId != null), ChequeAmount = p.Cheque == null ? (decimal?)null : p.Cheque.Amount,
            }).ToDictionaryAsync(p => p.Id, cancellationToken);
        foreach (var parcelId in parcelIds)
        {
            var parcel = parcels.GetValueOrDefault(parcelId) ?? throw BusinessException.NotFound($"Colis introuvable : {parcelId}", new { reason = "parcel-unknown", parcelId });
            if (parcel.StatusId != Billing.DeliveredStatusId)
            {
                throw BusinessException.Conflict($"Le colis {parcel.Code} n'est pas livré.", new { reason = "parcel-not-delivered", code = parcel.Code, statusId = parcel.StatusId });
            }
            if (parcel.FinancialStatus == FinancialStatus.NON_ENCAISSE)
            {
                throw BusinessException.Validation(
                    $"Aucun encaissement enregistré pour le colis {parcel.Code} : il n'y a pas d'argent à remettre. Confirmez d'abord la livraison sur le circuit.",
                    new { reason = "parcel-not-collected", code = parcel.Code });
            }
            if (parcel.CourierId != remittance.CourierId) throw BusinessException.Conflict($"Le colis {parcel.Code} n'est pas affecté à ce livreur.", new { reason = "parcel-other-courier", code = parcel.Code });
            if (parcel.Remitted) throw BusinessException.Conflict($"Le cash du colis {parcel.Code} a déjà été remis.", new { reason = "parcel-already-remitted", code = parcel.Code });
        }
        var deducts = remittance.SettlementMode == SettlementMode.AT_REMITTANCE;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var parcelId in parcelIds.Distinct())
        {
            var parcel = parcels[parcelId];
            var collected = Collected(parcel.CodAmount, parcel.DeliveryFee, parcel.FeeIncluded, parcel.AlreadyPaid);
            var chequeCents = Math.Min(Money.ToCents(parcel.ChequeAmount ?? 0m), collected);
            var line = await db.CashRemittanceParcels.FirstOrDefaultAsync(l => l.RemittanceId == remittance.Id && l.ParcelId == parcelId, cancellationToken);
            if (line is null)
            {
                line = new CashRemittanceParcel { RemittanceId = remittance.Id, ParcelId = parcelId };
                db.CashRemittanceParcels.Add(line);
            }
            line.Amount = Money.FromCents(collected);
            line.Cash = Money.FromCents(collected - chequeCents);
            line.Cheque = Money.FromCents(chequeCents);
            line.Commission = Money.FromCents(deducts ? Money.ToCents(parcel.CourierCommission ?? 0m) : 0);
        }
        await db.SaveChangesAsync(cancellationToken);
        await RemittanceLines.RefreshTotalAsync(db, remittance.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>`CourierBalanceService.balance` : ses écarts et règlements, les 200 derniers, et leur somme.</summary>
    public static async Task<CourierBalanceDto> BalanceAsync(IAppDbContext db, string courierId, CancellationToken cancellationToken)
    {
        var courier = await db.Couriers.AsNoTracking().Where(c => c.Id == courierId).Select(c => new { c.Id, c.FirstName, c.LastName }).FirstOrDefaultAsync(cancellationToken)
                      ?? throw BusinessException.NotFound("Livreur introuvable.");
        var entries = await db.CourierBalanceEntries.AsNoTracking().Where(e => e.CourierId == courierId).OrderByDescending(e => e.At).Take(200)
            .Select(e => new { e.Id, e.Kind, e.Amount, Code = e.Remittance != null ? e.Remittance.Code : null, e.Method, e.Note, e.AuthorName, e.At }).ToListAsync(cancellationToken);
        var sum = await db.CourierBalanceEntries.AsNoTracking().Where(e => e.CourierId == courierId).SumAsync(e => (decimal?)e.Amount, cancellationToken);
        return new CourierBalanceDto(courier.Id, $"{courier.FirstName} {courier.LastName}", Money.ToCentsOrNull(sum) ?? 0,
            entries.Select(e => new CourierBalanceEntryDto(e.Id, Money.ToWire(e.Kind), Money.ToCents(e.Amount), e.Code, e.Method is { } m ? Money.ToWire(m) : null, e.Note, e.AuthorName, e.At)).ToList());
    }
}
