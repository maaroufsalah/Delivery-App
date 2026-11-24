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

public class RemittanceListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public DateTime? CourierClosedAt { get; set; }
    public string? CourierClosedByName { get; set; }
    public string CourierId { get; set; } = string.Empty;
    public string CourierName { get; set; } = string.Empty;
    public string CityId { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;
    public string AgencyId { get; set; } = string.Empty;
    public string AgencyName { get; set; } = string.Empty;
    public int ParcelCount { get; set; }
    public long Amount { get; set; }
    public long TotalCash { get; set; }
    public long TotalCheque { get; set; }
    public int ChequeCount { get; set; }
    public long ExpectedCod { get; set; }
    public long ExpectedCommission { get; set; }
    public long ExpectedNet { get; set; }
    public long? DeclaredAmount { get; set; }
    public long Variance { get; set; }
    public string? VarianceReason { get; set; }
    public string SettlementMode { get; set; } = string.Empty;
    public DateTime? ConfirmedAt { get; set; }
    public string? ConfirmedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed record RemittanceChequeDto(string Id, string Number, string? Bank, long Amount, string Status, string ParcelId, string ParcelCode);

public sealed record RemittanceItemDto(string ParcelId, string ParcelCode, string ClientName, string RecipientName, long Amount, long Cash, long Cheque, IReadOnlyList<RemittanceChequeDto> Cheques,
    long Commission);

public sealed class RemittanceDetailDto : RemittanceListItemDto
{
    [JsonPropertyOrder(1)] public string? Note { get; set; }
    [JsonPropertyOrder(1)] public bool Confirmable { get; set; }
    [JsonPropertyOrder(1)] public bool Reopenable { get; set; }
    [JsonPropertyOrder(1)] public List<RemittanceItemDto> Items { get; set; } = [];
    [JsonPropertyOrder(1)] public List<RemittanceChequeDto> Cheques { get; set; } = [];
}

public sealed record RemittanceCandidateDto(string Id, string Code, string ClientName, string RecipientName, string DestinationCityName, long CodCollected, long CourierCommission, DateTime StatusAt);

public sealed record RemittanceSummaryTotalsDto(long Collected, long Cash, long Cheque, long ToHandOver, long Handed, long Variance, int Count, int OpenCount, int ConfirmedCount);

public sealed record RemittanceSummaryOpenDto(string RemittanceId, string Code, string CourierId, string CourierName, string AgencyName, long ToHandOver, long ChequeAmount, int ChequeCount,
    int ParcelCount, DateTime OpenedAt, DateTime? CourierClosedAt);

public sealed record RemittanceSummaryDto(string Period, string From, string To, string? CourierId, RemittanceSummaryTotalsDto Totals, IReadOnlyList<RemittanceSummaryOpenDto> Open);

/// <summary>`CourierOpenRemittance` : enveloppé, car « pas de caisse ouverte » est une réponse, pas un corps vide.</summary>
public sealed record CourierOpenRemittanceDto([property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] RemittanceDetailDto? Remittance);

public sealed record CourierBalanceEntryDto(string Id, string Kind, long Amount, string? RemittanceCode, string? Method, string? Note, string AuthorName, DateTime At);

public sealed record CourierBalanceDto(string CourierId, string CourierName, long Balance, IReadOnlyList<CourierBalanceEntryDto> Entries);

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

/// <summary>`remittanceListQuerySchema`.</summary>
public sealed record RemittanceListFilter(ListQuery Paging, string? Status, IReadOnlyList<string>? Stage, IReadOnlyList<string>? CourierId, IReadOnlyList<string>? CityId,
    IReadOnlyList<string>? AgencyId, string? From, string? To);

internal static class RemittanceLists
{
    public static IQueryable<CashRemittance> Filter(IAppDbContext db, ICurrentUser caller, RemittanceListFilter filter)
    {
        var issues = new List<object>();
        if (filter.Status is { } s && !Remittances.Statuses.Contains(s)) issues.Add(new { path = "status", message = Zod.InvalidOption(Remittances.Statuses) });
        if (filter.Stage?.Any(x => !Remittances.Stages.Contains(x)) == true) issues.Add(new { path = "stage", message = Zod.InvalidOption(Remittances.Stages) });
        if (issues.Count > 0) throw BusinessException.Validation("Saisie invalide.", new { issues });
        var where = Remittances.Scoped(db.CashRemittances.AsNoTracking(), caller);
        if (filter.Stage is { Count: > 0 } stages) where = Remittances.InStages(where, stages);
        else if (filter.Status is { } status) { var value = Enum.Parse<CashRemittanceStatus>(Money.FromWire(status)); where = where.Where(r => r.Status == value); }
        if (filter.CourierId is { } couriers && caller.MayFilterBy(caller.CourierId)) where = where.Where(r => couriers.Contains(r.CourierId));
        if (filter.CityId is { } cities) where = where.Where(r => cities.Contains(r.CityId));
        if (filter.AgencyId is { } agencies && caller.MayFilterBy(caller.AgencyId)) where = where.Where(r => agencies.Contains(r.AgencyId));
        var (from, to) = ReturnRows.DayRange(filter.From, filter.To);
        if (from is { } f) where = where.Where(r => r.CreatedAt >= f);
        if (to is { } t) where = where.Where(r => r.CreatedAt < t);
        if (filter.Paging.Search is { Length: > 0 } search)
        {
            var needle = search.ToLower();
            where = where.Where(r => r.Code.ToLower().Contains(needle) || r.Courier.FirstName.ToLower().Contains(needle) || r.Courier.LastName.ToLower().Contains(needle)
                                     || r.Parcels.Any(l => l.Parcel.Code.ToLower().Contains(needle) || l.Parcel.RecipientName.ToLower().Contains(needle)
                                                           || l.Parcel.RecipientPhone.ToLower().Contains(needle) || l.Parcel.Client.Name.ToLower().Contains(needle))
                                     || r.Cheques.Any(c => c.Number.ToLower().Contains(needle)));
        }
        return filter.Paging.Order == "asc" ? where.OrderBy(r => r.CreatedAt) : where.OrderByDescending(r => r.CreatedAt);
    }
}

/// <summary>`GET /remittances`.</summary>
public sealed record GetRemittancesQuery(RemittanceListFilter Filter) : IRequest<Paginated<RemittanceListItemDto>>;

internal sealed class GetRemittancesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetRemittancesQuery, Paginated<RemittanceListItemDto>>
{
    public async Task<Paginated<RemittanceListItemDto>> Handle(GetRemittancesQuery request, CancellationToken cancellationToken)
    {
        var where = RemittanceLists.Filter(db, caller, request.Filter);
        var paging = request.Filter.Paging;
        var total = await where.CountAsync(cancellationToken);
        var items = await Remittances.ListAsync(where.Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize), cancellationToken);
        return new Paginated<RemittanceListItemDto>(items, total, paging.Page, paging.PageSize);
    }
}

/// <summary>`GET /remittances/export` : classeur « Retours de fonds ».</summary>
public sealed record ExportRemittancesQuery(RemittanceListFilter Filter) : IRequest<FileContent>;

internal sealed class ExportRemittancesHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportRemittancesQuery, FileContent>
{
    private static readonly Dictionary<string, string> StageLabels = new()
    {
        ["open"] = "Caisse ouverte", ["closed"] = "Caisse fermée", ["received"] = "Caisse reçue", ["validated"] = "Caisse validée", ["cancelled"] = "Annulée",
    };

    private static readonly IReadOnlyList<ExportColumn<RemittanceListItemDto>> Columns =
    [
        new("Retour de fonds", r => r.Code),
        new("Statut", r => StageLabels[r.Stage]),
        new("Clôturée par le livreur le", r => Exports.DateTimeText(r.CourierClosedAt)),
        new("Livreur", r => r.CourierName),
        new("Ville", r => r.CityName),
        new("Agence", r => r.AgencyName),
        new("Colis", r => r.ParcelCount),
        new("COD encaissé", r => Exports.Amount(r.ExpectedCod)),
        new("Commissions déduites", r => Exports.Amount(r.ExpectedCommission)),
        new("Net attendu", r => Exports.Amount(r.ExpectedNet)),
        new("Montant remis", r => r.DeclaredAmount is null ? string.Empty : Exports.Amount(r.DeclaredAmount)),
        new("Écart", r => Exports.Amount(r.Variance)),
        new("Motif de l’écart", r => r.VarianceReason ?? string.Empty),
        new("Montant", r => Exports.Amount(r.Amount)),
        new("Confirmé le", r => Exports.DateTimeText(r.ConfirmedAt)),
        new("Confirmé par", r => r.ConfirmedByName ?? string.Empty),
        new("Créé le", r => Exports.DateTimeText(r.CreatedAt)),
    ];

    public async Task<FileContent> Handle(ExportRemittancesQuery request, CancellationToken cancellationToken)
    {
        var where = RemittanceLists.Filter(db, caller, request.Filter);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Retours de fonds", Columns, await Remittances.ListAsync(where, cancellationToken), cancellationToken);
    }
}

/// <summary>`remittanceCandidateQuerySchema` : les colis livrés et encaissés du livreur, pas encore remis.</summary>
public sealed record GetRemittanceCandidatesQuery(string? CourierId, string? CityId, string? From, string? To, string? Search) : IRequest<IReadOnlyList<RemittanceCandidateDto>>;

public sealed class GetRemittanceCandidatesValidator : ZodValidator<GetRemittanceCandidatesQuery>
{
    public GetRemittanceCandidatesValidator()
    {
        RuleFor(x => x.CourierId).Required().MinLength(1, "required");
        RuleFor(x => x.Search).MaxLength(120);
    }
}

internal sealed class GetRemittanceCandidatesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetRemittanceCandidatesQuery, IReadOnlyList<RemittanceCandidateDto>>
{
    public async Task<IReadOnlyList<RemittanceCandidateDto>> Handle(GetRemittanceCandidatesQuery request, CancellationToken cancellationToken)
    {
        var where = ParcelFilters.Scoped(db.Parcels.AsNoTracking(), caller)
            .Where(p => p.StatusId == Billing.DeliveredStatusId && p.FinancialStatus != FinancialStatus.NON_ENCAISSE && !p.Remittances.Any(l => l.ConfirmedParcelId != null));
        var courierId = caller.CourierId ?? (caller.MayFilterBy(caller.CourierId) ? request.CourierId : null);
        if (courierId is not null) where = where.Where(p => p.CourierId == courierId);
        if (!string.IsNullOrEmpty(request.CityId)) where = where.Where(p => p.DestinationCityId == request.CityId);
        var (from, to) = ReturnRows.DayRange(request.From, request.To);
        if (from is { } f) where = where.Where(p => p.StatusAt >= f);
        if (to is { } t) where = where.Where(p => p.StatusAt < t);
        if (!string.IsNullOrEmpty(request.Search))
        {
            var needle = request.Search.ToLower();
            where = where.Where(p => p.Code.ToLower().Contains(needle) || p.RecipientName.ToLower().Contains(needle));
        }
        var rows = await where.OrderBy(p => p.StatusAt).Take(NoteRules.SelectionMax + 1)
            .Select(p => new { p.Id, p.Code, Client = p.Client.Name, p.RecipientName, City = p.DestinationCity.Name, p.CodAmount, p.DeliveryFee, p.FeeIncluded, p.AlreadyPaid, p.CourierCommission, p.StatusAt })
            .ToListAsync(cancellationToken);
        return rows.Select(p => new RemittanceCandidateDto(p.Id, p.Code, p.Client, p.RecipientName, p.City, Remittances.Collected(p.CodAmount, p.DeliveryFee, p.FeeIncluded, p.AlreadyPaid),
            Money.ToCents(p.CourierCommission ?? 0m), p.StatusAt)).Where(c => c.CodCollected > 0).ToList();
    }
}

/// <summary>`remittanceSummaryQuerySchema` : les cartes au-dessus de la liste, sur la période.</summary>
public sealed record GetRemittanceSummaryQuery(string? Period, string? From, string? To, IReadOnlyList<string>? CourierId, IReadOnlyList<string>? AgencyId) : IRequest<RemittanceSummaryDto>;

public sealed class GetRemittanceSummaryValidator : ZodValidator<GetRemittanceSummaryQuery>
{
    public GetRemittanceSummaryValidator()
    {
        RuleFor(x => x.Period).OneOf(Periods.All);
    }
}

internal sealed class GetRemittanceSummaryHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetRemittanceSummaryQuery, RemittanceSummaryDto>
{
    public async Task<RemittanceSummaryDto> Handle(GetRemittanceSummaryQuery request, CancellationToken cancellationToken)
    {
        var courierIds = caller.CourierId is { } own ? [own] : request.CourierId;
        var agencyIds = caller.IsSuperAdmin() ? request.AgencyId : null;
        var scoped = Remittances.Scoped(db.CashRemittances.AsNoTracking(), caller);
        if (courierIds is not null) scoped = scoped.Where(r => courierIds.Contains(r.CourierId));
        if (agencyIds is not null) scoped = scoped.Where(r => agencyIds.Contains(r.AgencyId));
        var timeZone = await db.Companies.AsNoTracking().OrderBy(c => c.CreatedAt).Select(c => c.Timezone).FirstOrDefaultAsync(cancellationToken);
        var first = await scoped.OrderBy(r => r.CreatedAt).Select(r => (DateTime?)r.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        var window = Periods.Resolve(request.Period ?? "month", request.From, request.To, Periods.Zone(string.IsNullOrEmpty(timeZone) ? "Africa/Casablanca" : timeZone), first, clock.UtcNow);
        var rows = await scoped.Where(r => r.CreatedAt >= window.Start && r.CreatedAt < window.End && r.Status != CashRemittanceStatus.CANCELLED)
            .Select(r => new { r.Status, r.ExpectedCod, r.TotalCash, r.TotalCheque, r.ExpectedNet, r.DeclaredAmount, r.Variance }).ToListAsync(cancellationToken);
        var open = await scoped.Where(r => r.Status == CashRemittanceStatus.DRAFT).OrderBy(r => r.CreatedAt)
            .Select(r => new RemittanceSummaryOpenDto(r.Id, r.Code, r.CourierId, r.Courier.FirstName + " " + r.Courier.LastName, r.Agency.Name, (long)Math.Round(r.ExpectedNet * 100m),
                (long)Math.Round(r.TotalCheque * 100m), r.Cheques.Count, r.Parcels.Count, r.CreatedAt, r.CourierClosedAt)).ToListAsync(cancellationToken);
        // `_sum` par statut, puis la somme des sommes : chaque statut arrondi au centime avant d'être additionné.
        var totals = new RemittanceSummaryTotalsDto(
            rows.GroupBy(r => r.Status).Sum(g => Money.ToCents(g.Sum(r => r.ExpectedCod))),
            rows.GroupBy(r => r.Status).Sum(g => Money.ToCents(g.Sum(r => r.TotalCash))),
            rows.GroupBy(r => r.Status).Sum(g => Money.ToCents(g.Sum(r => r.TotalCheque))),
            rows.GroupBy(r => r.Status).Sum(g => Money.ToCents(g.Sum(r => r.ExpectedNet))),
            rows.GroupBy(r => r.Status).Sum(g => Money.ToCents(g.Sum(r => r.DeclaredAmount ?? 0m))),
            rows.GroupBy(r => r.Status).Sum(g => Money.ToCents(g.Sum(r => r.Variance))),
            rows.Count, rows.Count(r => r.Status == CashRemittanceStatus.DRAFT), rows.Count(r => r.Status == CashRemittanceStatus.CONFIRMED));
        return new RemittanceSummaryDto(window.Period, window.From, window.To, courierIds?.Count == 1 ? courierIds[0] : null, totals, open);
    }
}

/// <summary>`GET /remittances/:code`.</summary>
public sealed record GetRemittanceQuery(string Code) : IRequest<RemittanceDetailDto>;

internal sealed class GetRemittanceHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetRemittanceQuery, RemittanceDetailDto>
{
    public Task<RemittanceDetailDto> Handle(GetRemittanceQuery request, CancellationToken cancellationToken) => Remittances.DetailAsync(db, caller, request.Code, cancellationToken);
}

/// <summary>`remittanceCreateSchema` : un retour de fonds manuel, ouvert avec ses colis.</summary>
public sealed record CreateRemittanceCommand : IRequest<RemittanceDetailDto>
{
    public string? CourierId { get; init; }
    public string? CityId { get; init; }
    public string? AgencyId { get; init; }
    public string? Note { get; init => field = Zod.Trim(value); }
    public IReadOnlyList<string>? ParcelIds { get; init; }
}

public sealed class CreateRemittanceValidator : ZodValidator<CreateRemittanceCommand>
{
    public CreateRemittanceValidator()
    {
        RuleFor(x => x.CourierId).Required().MinLength(1, "required");
        RuleFor(x => x.CityId).Required().MinLength(1, "required");
        RuleFor(x => x.AgencyId).Required().MinLength(1, "required");
        RuleFor(x => x.Note).MaxLength(500);
        RuleFor(x => x.ParcelIds).Custom((ids, context) => RemittanceIds.Check(ids, context, required: false));
    }
}

internal static class RemittanceIds
{
    public static void Check<T>(IReadOnlyList<string>? ids, ValidationContext<T> context, bool required)
    {
        if (ids is null) return;
        if (required && ids.Count < 1) context.AddFailure(ValidationPaths.Raw + "parcelIds", "required");
        if (ids.Count > NoteRules.SelectionMax) context.AddFailure(ValidationPaths.Raw + "parcelIds", "too-many");
        for (var i = 0; i < ids.Count; i++) if (string.IsNullOrEmpty(ids[i])) context.AddFailure(ValidationPaths.Raw + $"parcelIds.{i}", Zod.TooSmallString(1));
    }
}

internal sealed class CreateRemittanceHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CreateRemittanceCommand, RemittanceDetailDto>
{
    public async Task<RemittanceDetailDto> Handle(CreateRemittanceCommand input, CancellationToken cancellationToken)
    {
        await ReturnRows.AssertAgencyReachableAsync(db, caller, input.AgencyId!, cancellationToken);
        if (!await db.Couriers.AnyAsync(c => c.Id == input.CourierId && c.DeletedAt == null, cancellationToken)) throw BusinessException.NotFound("Livreur introuvable.");
        if (input.ParcelIds is not { Count: > 0 }) throw BusinessException.Validation("Un retour de fonds s’ouvre avec au moins un colis.", new { reason = "remittance-needs-parcels" });
        if (await Remittances.OtherOpenCodeAsync(db, input.CourierId!, cancellationToken) is { } open)
        {
            throw BusinessException.Validation($"Ce livreur a déjà un retour de fonds ouvert : {open}.", new { reason = "courier-has-open-remittance", code = open });
        }
        if (!await db.Cities.AnyAsync(c => c.Id == input.CityId && c.DeletedAt == null, cancellationToken)) throw BusinessException.NotFound("Ville introuvable.");
        var now = DateTime.UtcNow;
        var prefix = await db.SettingAsync(SettingKeys.RemittancePrefix, cancellationToken) is { ValueKind: JsonValueKind.String } p ? p.GetString()! : RemittanceLines.DefaultPrefix;
        var yearly = await db.BoolSettingAsync(SettingKeys.NumberingYearlyReset, false, cancellationToken);
        CashRemittance created;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var code = await DocumentCodes.ReserveAsync(db, prefix, now, cancellationToken, yearly);
            created = new CashRemittance
            {
                Code = code, CourierId = input.CourierId!, CityId = input.CityId!, AgencyId = input.AgencyId!, Note = ReturnRows.Text(input.Note), CreatedById = caller.UserId, CreatedAt = now,
                UpdatedAt = now,
            };
            db.CashRemittances.Add(created);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                throw BusinessException.Validation("Ce livreur a déjà un retour de fonds ouvert.", new { reason = "courier-has-open-remittance" });
            }
            await transaction.CommitAsync(cancellationToken);
        }
        db.ChangeTracker.Clear();
        try
        {
            await Remittances.AddParcelsAsync(db, caller, created.Code, input.ParcelIds, cancellationToken);
        }
        catch (BusinessException)
        {
            db.ChangeTracker.Clear();
            await db.CashRemittances.Where(r => r.Id == created.Id).ExecuteDeleteAsync(cancellationToken);
            throw;
        }
        return await Remittances.DetailAsync(db, caller, created.Code, cancellationToken);
    }
}

/// <summary>`remittanceParcelsSchema`.</summary>
public sealed record AddRemittanceParcelsCommand : IRequest<RemittanceDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public IReadOnlyList<string>? ParcelIds { get; init; }
}

public sealed class AddRemittanceParcelsValidator : ZodValidator<AddRemittanceParcelsCommand>
{
    public AddRemittanceParcelsValidator()
    {
        RuleFor(x => x.ParcelIds).Required("array").Custom((ids, context) => RemittanceIds.Check(ids, context, required: true));
    }
}

internal sealed class AddRemittanceParcelsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<AddRemittanceParcelsCommand, RemittanceDetailDto>
{
    public async Task<RemittanceDetailDto> Handle(AddRemittanceParcelsCommand input, CancellationToken cancellationToken)
    {
        await Remittances.AddParcelsAsync(db, caller, input.Code, input.ParcelIds!, cancellationToken);
        return await Remittances.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}

/// <summary>`DELETE /remittances/:code/parcels/:parcelId`.</summary>
public sealed record RemoveRemittanceParcelCommand(string Code, string ParcelId) : IRequest<RemittanceDetailDto>;

internal sealed class RemoveRemittanceParcelHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RemoveRemittanceParcelCommand, RemittanceDetailDto>
{
    public async Task<RemittanceDetailDto> Handle(RemoveRemittanceParcelCommand request, CancellationToken cancellationToken)
    {
        var remittance = await Remittances.RequireDraftAsync(db, caller, request.Code, cancellationToken);
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await db.CashRemittanceParcels.Where(l => l.RemittanceId == remittance.Id && l.ParcelId == request.ParcelId).ExecuteDeleteAsync(cancellationToken);
            await RemittanceLines.RefreshTotalAsync(db, remittance.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return await Remittances.DetailAsync(db, caller, request.Code, cancellationToken);
    }
}

/// <summary>`remittanceCountSchema` : le montant remis se saisit ; un écart demande un motif.</summary>
public sealed record CountRemittanceCommand : IRequest<RemittanceDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public double? DeclaredAmount { get; init; }
    public string? VarianceReason { get; init => field = Zod.Trim(value); }
    public double? ExpectedNet { get; init; }
}

public sealed class CountRemittanceValidator : ZodValidator<CountRemittanceCommand>
{
    public CountRemittanceValidator()
    {
        RuleFor(x => x.DeclaredAmount).MadAmount0();
        RuleFor(x => x.ExpectedNet).MadAmount0();
        RuleFor(x => x.VarianceReason).MaxLength(300);
    }

    protected override void Refine(CountRemittanceCommand value, Action<string, string> issue)
    {
        if (value.DeclaredAmount is not null && value.ExpectedNet is not null && value.DeclaredAmount != value.ExpectedNet && string.IsNullOrEmpty(value.VarianceReason))
        {
            issue(ValidationPaths.Raw + "varianceReason", "variance-needs-reason");
        }
    }
}

internal sealed class CountRemittanceHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CountRemittanceCommand, RemittanceDetailDto>
{
    public async Task<RemittanceDetailDto> Handle(CountRemittanceCommand input, CancellationToken cancellationToken)
    {
        if (caller.CourierId is not null) throw BusinessException.Forbidden("Seule l’agence compte un retour de fonds.", new { reason = "counting-is-agency-only" });
        var remittance = await Remittances.RequireDraftAsync(db, caller, input.Code, cancellationToken);
        var declared = (long)Math.Round(input.DeclaredAmount!.Value * 100, MidpointRounding.AwayFromZero);
        var lines = await db.CashRemittanceParcels.AsNoTracking().Where(l => l.RemittanceId == remittance.Id).Select(l => new { l.Amount, l.Cheque, l.Commission }).ToListAsync(cancellationToken);
        var net = Remittances.Sum(lines.Select(l => (l.Amount, l.Cheque, l.Commission))).Net;
        if (declared != net && string.IsNullOrEmpty(input.VarianceReason))
        {
            throw BusinessException.Validation("Un écart entre le montant remis et le net attendu demande un motif.", new { field = "varianceReason" });
        }
        var amount = Money.FromCents(declared);
        var reason = declared == net ? null : input.VarianceReason;
        await db.CashRemittances.Where(r => r.Id == remittance.Id).ExecuteUpdateAsync(set => set.SetProperty(r => r.DeclaredAmount, amount).SetProperty(r => r.VarianceReason, reason)
            .SetProperty(r => r.Amount, amount).SetProperty(r => r.UpdatedAt, DateTime.UtcNow), cancellationToken);
        return await Remittances.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}

/// <summary>`GET /remittances/:code/export` : les colis du bon.</summary>
public sealed record ExportRemittanceParcelsQuery(string Code) : IRequest<FileContent>;

internal sealed class ExportRemittanceParcelsHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportRemittanceParcelsQuery, FileContent>
{
    private sealed record Line(RemittanceItemDto Item, string? ExternalCode, string RecipientPhone, string DestinationCityName, int StatusId);

    private static readonly IReadOnlyList<ExportColumn<Line>> Columns =
    [
        new("Code", r => r.Item.ParcelCode, Text: true),
        new("Référence commande", r => r.ExternalCode ?? string.Empty, Text: true),
        new("Vendeur", r => r.Item.ClientName),
        new("Destinataire", r => r.Item.RecipientName),
        new("Téléphone", r => r.RecipientPhone, Text: true),
        new("Ville", r => r.DestinationCityName),
        new("Statut", r => DocumentLabels.ParcelStatus(r.StatusId)),
        new("Encaissé", r => Exports.Amount(r.Item.Amount)),
        new("Espèces", r => Exports.Amount(r.Item.Cash)),
        new("Chèque", r => Exports.Amount(r.Item.Cheque)),
        new("N° de chèque", r => string.Join(", ", r.Item.Cheques.Select(c => c.Number)), Text: true),
        new("Commission", r => Exports.Amount(r.Item.Commission)),
    ];

    public async Task<FileContent> Handle(ExportRemittanceParcelsQuery request, CancellationToken cancellationToken)
    {
        var remittance = await Remittances.DetailAsync(db, caller, request.Code, cancellationToken);
        var ids = remittance.Items.Select(i => i.ParcelId).ToList();
        var extras = await db.Parcels.AsNoTracking().Where(p => ids.Contains(p.Id)).Select(p => new { p.Id, p.ExternalCode, p.RecipientPhone, City = p.DestinationCity.Name, p.StatusId })
            .ToDictionaryAsync(p => p.Id, cancellationToken);
        var rows = remittance.Items.Select(i => extras.GetValueOrDefault(i.ParcelId) is { } e ? new Line(i, e.ExternalCode, e.RecipientPhone, e.City, e.StatusId) : new Line(i, null, string.Empty, string.Empty, 0))
            .ToList();
        return await Exports.ToXlsxAsync(db, excel, clock, caller, $"Caisse {remittance.Code}", Columns, rows, cancellationToken);
    }
}

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

/// <summary>`POST /remittances/:code/reopen` : l'agence rouvre une caisse fermée par le livreur, s'il n'en a pas ouvert d'autre.</summary>
public sealed record ReopenRemittanceCommand(string Code) : IRequest<RemittanceDetailDto>;

internal sealed class ReopenRemittanceHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<ReopenRemittanceCommand, RemittanceDetailDto>
{
    public async Task<RemittanceDetailDto> Handle(ReopenRemittanceCommand request, CancellationToken cancellationToken)
    {
        if (caller.CourierId is not null) throw BusinessException.Forbidden("Seule l’agence rouvre une caisse.", new { reason = "reopening-is-agency-only" });
        var remittance = await Remittances.Scoped(db.CashRemittances.AsNoTracking(), caller).Where(r => r.Code == request.Code)
                             .Select(r => new { r.Id, r.Status, r.CourierId, r.AgencyId, r.CourierClosedAt }).FirstOrDefaultAsync(cancellationToken)
                         ?? throw BusinessException.NotFound($"Retour de fonds introuvable : {request.Code}");
        if (remittance.Status != CashRemittanceStatus.DRAFT) throw BusinessException.Conflict("Cette caisse est reçue : elle ne se rouvre plus.", new { reason = "remittance-confirmed" });
        if (remittance.CourierClosedAt is null) throw BusinessException.Conflict("Cette caisse est déjà ouverte.", new { reason = "remittance-not-closed" });
        await ReturnRows.AssertAgencyReachableAsync(db, caller, remittance.AgencyId, cancellationToken);
        if (await Remittances.OtherOpenCodeAsync(db, remittance.CourierId, cancellationToken) is { } other) throw Remittances.HasNewerOpen(other);
        int moved;
        try
        {
            moved = await db.CashRemittances.Where(r => r.Id == remittance.Id && r.Status == CashRemittanceStatus.DRAFT && r.CourierClosedAt != null)
                .ExecuteUpdateAsync(set => set.SetProperty(r => r.CourierClosedAt, (DateTime?)null).SetProperty(r => r.CourierClosedById, (string?)null), cancellationToken);
        }
        catch (Npgsql.PostgresException error) when (error.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation)
        {
            throw Remittances.HasNewerOpen(await Remittances.OtherOpenCodeAsync(db, remittance.CourierId, cancellationToken) ?? string.Empty);
        }
        if (moved == 0) throw BusinessException.Conflict("Cette caisse n’est plus fermée.", new { reason = "remittance-not-closed" });
        return await Remittances.DetailAsync(db, caller, request.Code, cancellationToken);
    }
}

/// <summary>`DELETE /remittances/:code` : tout bon non confirmé.</summary>
public sealed record DeleteRemittanceCommand(string Code) : IRequest<ReturnCodeDto>;

internal sealed class DeleteRemittanceHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<DeleteRemittanceCommand, ReturnCodeDto>
{
    public async Task<ReturnCodeDto> Handle(DeleteRemittanceCommand request, CancellationToken cancellationToken)
    {
        var remittance = await Remittances.Scoped(db.CashRemittances.AsNoTracking(), caller).Where(r => r.Code == request.Code).Select(r => new { r.Id, r.Status }).FirstOrDefaultAsync(cancellationToken)
                         ?? throw BusinessException.NotFound($"Retour de fonds introuvable : {request.Code}");
        if (remittance.Status == CashRemittanceStatus.CONFIRMED) throw BusinessException.Conflict("Un retour de fonds confirmé ne se supprime pas.", new { reason = "remittance-confirmed" });
        await db.CashRemittances.Where(r => r.Id == remittance.Id).ExecuteDeleteAsync(cancellationToken);
        return new ReturnCodeDto(request.Code);
    }
}

/// <summary>`GET /courier/me/remittance` : sa caisse ouverte, ou `null`.</summary>
public sealed record GetMyOpenRemittanceQuery : IRequest<CourierOpenRemittanceDto>;

internal sealed class GetMyOpenRemittanceHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetMyOpenRemittanceQuery, CourierOpenRemittanceDto>
{
    public async Task<CourierOpenRemittanceDto> Handle(GetMyOpenRemittanceQuery request, CancellationToken cancellationToken)
    {
        var code = await Remittances.Scoped(db.CashRemittances.AsNoTracking(), caller).Where(r => r.Status == CashRemittanceStatus.DRAFT && r.CourierClosedAt == null).OrderBy(r => r.CreatedAt)
            .Select(r => r.Code).FirstOrDefaultAsync(cancellationToken);
        return new CourierOpenRemittanceDto(code is null ? null : await Remittances.DetailAsync(db, caller, code, cancellationToken));
    }
}

/// <summary>`POST /courier/me/remittance/close` : le livreur ferme sa caisse ; l'agence comptera et confirmera.</summary>
public sealed record CloseMyRemittanceCommand : IRequest<CourierOpenRemittanceDto>;

internal sealed class CloseMyRemittanceHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CloseMyRemittanceCommand, CourierOpenRemittanceDto>
{
    public async Task<CourierOpenRemittanceDto> Handle(CloseMyRemittanceCommand request, CancellationToken cancellationToken)
    {
        if (caller.CourierId is not { } courierId) throw BusinessException.Forbidden("Seul le livreur clôture sa propre caisse.", new { reason = "closing-own-cash-only" });
        var open = await db.CashRemittances.AsNoTracking().Where(r => r.CourierId == courierId && r.Status == CashRemittanceStatus.DRAFT && r.CourierClosedAt == null)
                       .Select(r => new { r.Id, r.Code, Lines = r.Parcels.Count }).FirstOrDefaultAsync(cancellationToken)
                   ?? throw BusinessException.Conflict("Aucune caisse ouverte à clôturer.", new { reason = "no-open-remittance" });
        if (open.Lines == 0) throw BusinessException.Validation("Une caisse vide ne se clôture pas.", new { reason = "remittance-empty" });
        var userId = caller.UserId;
        var closed = await db.CashRemittances.Where(r => r.Id == open.Id && r.Status == CashRemittanceStatus.DRAFT && r.CourierClosedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(r => r.CourierClosedAt, DateTime.UtcNow).SetProperty(r => r.CourierClosedById, userId), cancellationToken);
        if (closed == 0) throw BusinessException.Conflict("Cette caisse vient d’être clôturée.", new { reason = "remittance-already-closed" });
        return new CourierOpenRemittanceDto(await Remittances.DetailAsync(db, caller, open.Code, cancellationToken));
    }
}

/// <summary>`GET /courier/me/balance` : son solde, sans identifiant (la portée le donne).</summary>
public sealed record GetMyBalanceQuery : IRequest<CourierBalanceDto>;

internal sealed class GetMyBalanceHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetMyBalanceQuery, CourierBalanceDto>
{
    public Task<CourierBalanceDto> Handle(GetMyBalanceQuery request, CancellationToken cancellationToken) => Remittances.BalanceAsync(db, caller.CourierId ?? string.Empty, cancellationToken);
}

/// <summary>`GET /couriers/:id/balance`.</summary>
public sealed record GetCourierBalanceQuery(string CourierId) : IRequest<CourierBalanceDto>;

internal sealed class GetCourierBalanceHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetCourierBalanceQuery, CourierBalanceDto>
{
    public Task<CourierBalanceDto> Handle(GetCourierBalanceQuery request, CancellationToken cancellationToken) =>
        Remittances.BalanceAsync(db, caller.CourierId ?? request.CourierId, cancellationToken);
}

/// <summary>`courierSettlementSchema` : le règlement d'un écart, strictement positif, en centimes.</summary>
public sealed record SettleCourierBalanceCommand : IRequest<CourierBalanceDto>
{
    [JsonIgnore]
    public string CourierId { get; init; } = string.Empty;

    public JsonElement? Amount { get; init; }
    public string? Method { get; init; }
    public string? Note { get; init => field = Zod.Trim(value); }
}

public sealed class SettleCourierBalanceValidator : ZodValidator<SettleCourierBalanceCommand>
{
    public SettleCourierBalanceValidator()
    {
        RuleFor(x => x.Amount).Custom((amount, context) =>
        {
            if (amount is not { ValueKind: JsonValueKind.Number } value) { context.AddFailure(ValidationPaths.Raw + "amount", Zod.Expected("number", Zod.Received(amount))); return; }
            var number = value.GetDouble();
            if (number != Math.Floor(number)) context.AddFailure(ValidationPaths.Raw + "amount", Zod.ExpectedInt);
            else if (number <= 0) context.AddFailure(ValidationPaths.Raw + "amount", "Too small: expected number to be >0");
        });
        RuleFor(x => x.Method).OneOf(["cash", "transfer", "check"]);
        RuleFor(x => x.Note).MaxLength(500);
    }
}

internal sealed class SettleCourierBalanceHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<SettleCourierBalanceCommand, CourierBalanceDto>
{
    public async Task<CourierBalanceDto> Handle(SettleCourierBalanceCommand input, CancellationToken cancellationToken)
    {
        if (caller.CourierId is not null) throw BusinessException.Forbidden("Seule l’agence enregistre un règlement d’écart.", new { reason = "settlement-is-agency-only" });
        if (!await db.Couriers.AnyAsync(c => c.Id == input.CourierId, cancellationToken)) throw BusinessException.NotFound("Livreur introuvable.");
        db.CourierBalanceEntries.Add(new CourierBalanceEntry
        {
            CourierId = input.CourierId, Kind = CourierBalanceEntryKind.SETTLEMENT, Amount = Money.FromCents(input.Amount!.Value.GetInt64()),
            Method = Enum.Parse<PaymentMethod>((input.Method ?? "cash").ToUpperInvariant()), Note = ReturnRows.Text(input.Note), AuthorId = caller.UserId,
            AuthorName = await Remittances.AuthorNameAsync(db, caller.UserId, cancellationToken), At = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
        return await Remittances.BalanceAsync(db, input.CourierId, cancellationToken);
    }
}

/// <summary>`GET /remittances/:code/pdf` : le reçu de remise.</summary>
public sealed record GetRemittancePdfQuery(string Code) : IRequest<FileContent>;

internal sealed class GetRemittancePdfHandler(IAppDbContext db, ICurrentUser caller, IPdfService pdf) : IRequestHandler<GetRemittancePdfQuery, FileContent>
{
    public async Task<FileContent> Handle(GetRemittancePdfQuery request, CancellationToken cancellationToken)
    {
        var r = await Remittances.DetailAsync(db, caller, request.Code, cancellationToken);
        var company = await PdfCompany.LoadAsync(db, "Reçu de remise", cancellationToken);
        var sheet = new RemittanceSheet(company, r.Code, r.Status, r.AgencyName, r.CourierName, r.CityName, r.CreatedAt, r.SettlementMode, r.ParcelCount, r.ExpectedCod, r.TotalCheque,
            r.ChequeCount, r.TotalCash, r.ExpectedCommission, r.ExpectedNet, r.DeclaredAmount, r.Variance, r.VarianceReason, r.ConfirmedByName,
            r.Items.Select(i => new RemittanceSheetLine(i.ParcelCode, i.ClientName, i.RecipientName, i.Amount, i.Commission)).ToList(),
            r.Cheques.Select(c => new RemittanceSheetCheque(c.Number, c.Bank, c.ParcelCode, c.Amount)).ToList());
        return new FileContent(pdf.Render(sheet), FileContent.Pdf, $"{r.Code}.pdf", CacheControl: "private, no-store");
    }
}
