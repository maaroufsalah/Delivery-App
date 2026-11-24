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

namespace Delivery.Application.Finance;

public class CodPayoutListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string AgencyId { get; set; } = string.Empty;
    public string AgencyName { get; set; } = string.Empty;
    public int ParcelCount { get; set; }
    public long Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string? BankAccountId { get; set; }
    public string? BankAccountLabel { get; set; }
    public string? Reference { get; set; }
    public string? ProofName { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? PaidByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed record CodPayoutItemDto(string ParcelId, string ParcelCode, string RecipientName, string DestinationCityName, long Amount);

public sealed record CodPayoutChequeDto(string Id, string Number, string? Bank, long Amount, string ParcelCode, string Status, bool IncludedInPayout);

public sealed class CodPayoutDetailDto : CodPayoutListItemDto
{
    [JsonPropertyOrder(1)] public string? Note { get; set; }
    [JsonPropertyOrder(1)] public bool Payable { get; set; }
    [JsonPropertyOrder(1)] public List<CodPayoutItemDto> Items { get; set; } = [];
    [JsonPropertyOrder(1)] public List<CodPayoutChequeDto> Cheques { get; set; } = [];
}

public sealed record CodPayoutCandidateDto(string Id, string Code, string RecipientName, string DestinationCityName, long CodCollected, DateTime? ReimbursedAt, DateTime StatusAt);

public sealed record CodPayoutBulkCandidateDto(string ClientId, string ClientName, string AgencyId, string AgencyName, int ParcelCount, long Cash, int ChequeCount, long ChequeAmount, long Fees,
    long Total);

public sealed record CodPayoutBulkCreatedDto(string ClientId, string ClientName, string AgencyName, string Code, int ParcelCount, long Amount);

public sealed record CodPayoutBulkSkippedDto(string ClientId, string ClientName, string Reason);

public sealed record CodPayoutBulkResultDto(IReadOnlyList<CodPayoutBulkCreatedDto> Created, IReadOnlyList<CodPayoutBulkSkippedDto> Skipped, long Total);

/// <summary>Ce que le chèque d'un colis retranche de ce que la société verse (`chequePaidOn`) : lu par le versement et par la facture.</summary>
public static class ParcelCheques
{
    public sealed record Remitted(decimal Cheque, string? ConfirmedParcelId);

    public static long PaidOn(decimal? chequeAmount, ChequeStatus? status, IReadOnlyList<Remitted> remitted)
    {
        var line = remitted.FirstOrDefault(r => r.ConfirmedParcelId is not null) ?? remitted.FirstOrDefault();
        return CourierPay.ChequePaidOn(chequeAmount is null ? null : Money.ToCents(chequeAmount.Value), status == ChequeStatus.ANNULE, line is null ? null : Money.ToCents(line.Cheque));
    }

    public static long Payable(decimal codAmount, decimal deliveryFee, bool feeIncluded, bool alreadyPaid, decimal? chequeAmount, ChequeStatus? status, IReadOnlyList<Remitted> remitted) =>
        Billing.CodPayoutAmount(Billing.CodToCollect(Money.ToCents(codAmount), Money.ToCents(deliveryFee), feeIncluded, alreadyPaid), PaidOn(chequeAmount, status, remitted));
}

/// <summary>`CodPayoutsService` : portée, projections, montants versables.</summary>
public static class CodPayouts
{
    public const string DefaultPrefix = "BV";

    /// <summary>`payoutWhere` : administrateur tout, vendeur les siens, livreur rien, agent son agence.</summary>
    public static IQueryable<CodPayout> Scoped(IQueryable<CodPayout> payouts, ICurrentUser caller)
    {
        if (caller.IsSuperAdmin()) return payouts;
        if (caller.ClientId is { } clientId) return payouts.Where(p => p.ClientId == clientId);
        if (caller.CourierId is not null) return payouts.Where(p => p.Id == Scope.DenyAllId);
        if (caller.AgencyId is { } agencyId) return payouts.Where(p => p.AgencyId == agencyId);
        return payouts.Where(p => p.Id == Scope.DenyAllId);
    }

    public static IQueryable<CodPayoutListItemDto> Project(IQueryable<CodPayout> payouts) => payouts.Select(p => new CodPayoutListItemDto
    {
        Id = p.Id, Code = p.Code, Status = p.Status == CodPayoutStatus.DRAFT ? "draft" : "paid", ClientId = p.ClientId, ClientName = p.Client.Name, AgencyId = p.AgencyId, AgencyName = p.Agency.Name,
        ParcelCount = p.Parcels.Count, Amount = (long)Math.Round(p.Amount * 100m), Method = p.Method.ToString(), BankAccountId = p.BankAccountId,
        BankAccountLabel = p.BankAccount != null ? p.BankAccount.Label : null, Reference = p.Reference, ProofName = p.ProofName, PaidAt = p.PaidAt,
        PaidByName = p.PaidBy != null ? p.PaidBy.FirstName + " " + p.PaidBy.LastName : null, CreatedAt = p.CreatedAt,
    });

    public static T Wire<T>(T row) where T : CodPayoutListItemDto
    {
        row.Method = Money.ToWire(row.Method);
        return row;
    }

    public static async Task<CodPayoutDetailDto> DetailAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken)
    {
        var scoped = Scoped(db.CodPayouts.AsNoTracking(), caller).Where(p => p.Code == code);
        var row = Wire(await Project(scoped).FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound($"Bon de versement introuvable : {code}"));
        var extra = await scoped.Select(p => new { p.Note, p.Status }).FirstAsync(cancellationToken);
        var links = await db.CodPayoutParcels.AsNoTracking().Where(l => l.PayoutId == row.Id)
            .Select(l => new
            {
                l.ParcelId, l.Amount, l.Parcel.Code, l.Parcel.RecipientName, City = l.Parcel.DestinationCity.Name, l.Parcel.CodAmount, l.Parcel.DeliveryFee, l.Parcel.FeeIncluded,
                l.Parcel.AlreadyPaid,
                Cheque = l.Parcel.Cheque == null ? null : new { l.Parcel.Cheque.Id, l.Parcel.Cheque.Number, l.Parcel.Cheque.Bank, l.Parcel.Cheque.Amount, l.Parcel.Cheque.Status },
                Remitted = l.Parcel.Remittances.Where(r => r.Remittance.Status != CashRemittanceStatus.CANCELLED).Select(r => new ParcelCheques.Remitted(r.Cheque, r.ConfirmedParcelId)).ToList(),
            }).ToListAsync(cancellationToken);
        var detail = new CodPayoutDetailDto
        {
            Id = row.Id, Code = row.Code, Status = row.Status, ClientId = row.ClientId, ClientName = row.ClientName, AgencyId = row.AgencyId, AgencyName = row.AgencyName, ParcelCount = row.ParcelCount,
            Amount = row.Amount, Method = row.Method, BankAccountId = row.BankAccountId, BankAccountLabel = row.BankAccountLabel, Reference = row.Reference, ProofName = row.ProofName,
            PaidAt = row.PaidAt, PaidByName = row.PaidByName, CreatedAt = row.CreatedAt, Note = extra.Note, Payable = extra.Status == CodPayoutStatus.DRAFT && links.Count > 0,
            Items = links.Select(l => new CodPayoutItemDto(l.ParcelId, l.Code, l.RecipientName, l.City, Money.ToCents(l.Amount))).ToList(),
        };
        detail.Cheques = links.Where(l => l.Cheque is not null && l.Cheque.Status != ChequeStatus.ANNULE).Select(l => new CodPayoutChequeDto(l.Cheque!.Id, l.Cheque.Number, l.Cheque.Bank,
            Money.ToCents(l.Cheque.Amount), l.Code, Money.ToWire(l.Cheque.Status),
            Money.ToCents(l.Amount) > ParcelCheques.Payable(l.CodAmount, l.DeliveryFee, l.FeeIncluded, l.AlreadyPaid, l.Cheque.Amount, l.Cheque.Status, l.Remitted))).ToList();
        return detail;
    }

    public static async Task RefreshTotalAsync(IAppDbContext db, string payoutId, CancellationToken cancellationToken)
    {
        var total = (await db.CodPayoutParcels.AsNoTracking().Where(l => l.PayoutId == payoutId).Select(l => l.Amount).ToListAsync(cancellationToken)).Sum(a => Money.ToCents(a));
        await db.CodPayouts.Where(p => p.Id == payoutId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Amount, Money.FromCents(total)).SetProperty(p => p.UpdatedAt, DateTime.UtcNow), cancellationToken);
    }

    public static BusinessException AlreadyInvoiced(string parcelCode, string invoiceCode) => BusinessException.Conflict(
        $"Le COD du colis {parcelCode} est déjà inclus dans la facture {invoiceCode} (client facturé en régime normal) : il ne peut pas être reversé une seconde fois par un bon de versement.",
        new { reason = "parcel-cod-already-invoiced", code = parcelCode, invoiceCode });

    public static async Task<(string Prefix, bool Yearly)> NumberingAsync(IAppDbContext db, CancellationToken cancellationToken) =>
        (await db.SettingAsync(SettingKeys.CodPayoutPrefix, cancellationToken) is { ValueKind: JsonValueKind.String } p ? p.GetString()! : DefaultPrefix,
            await db.BoolSettingAsync(SettingKeys.NumberingYearlyReset, false, cancellationToken));

    private sealed record BulkRow(string Id, string ClientId, string ClientName, string AgencyId, string AgencyName, decimal CodAmount, decimal DeliveryFee, bool FeeIncluded, bool AlreadyPaid,
        decimal? ChequeAmount, ChequeStatus? ChequeStatus, List<ParcelCheques.Remitted> Remitted, DateTime StatusAt, string Code);

    /// <summary>`bulkWhere` : livrés, cash remis, jamais versés, non facturés en régime normal, vendeurs professionnels.</summary>
    private static IQueryable<Parcel> BulkWhere(IAppDbContext db, ICurrentUser caller, string? from, string? to, string? agencyId, string? clientId)
    {
        var where = ParcelFilters.Scoped(db.Parcels.AsNoTracking(), caller).Where(p => p.StatusId == Billing.DeliveredStatusId && p.ReimbursedByCourier && !p.CodPayouts.Any()
            && !p.InvoiceLines.Any(l => l.InvoicedParcelId != null && l.Invoice.BillingType == BillingType.NORMAL) && p.Client.BillingType == BillingType.PROFESSIONAL && p.Client.DeletedAt == null);
        if (clientId is not null) where = where.Where(p => p.ClientId == clientId);
        if (!string.IsNullOrEmpty(agencyId) && caller.MayFilterBy(caller.AgencyId)) where = where.Where(p => p.AgencyId == agencyId);
        var (start, end) = ReturnRows.DayRange(from, to);
        if (start is { } s) where = where.Where(p => p.StatusAt >= s);
        if (end is { } e) where = where.Where(p => p.StatusAt < e);
        return where;
    }

    private static Task<List<BulkRow>> BulkRowsAsync(IQueryable<Parcel> where, CancellationToken cancellationToken) => where.OrderBy(p => p.StatusAt).ThenBy(p => p.Code)
        .Select(p => new BulkRow(p.Id, p.ClientId, p.Client.Name, p.AgencyId, p.Agency.Name, p.CodAmount, p.DeliveryFee, p.FeeIncluded, p.AlreadyPaid,
            p.Cheque == null ? null : p.Cheque.Amount, p.Cheque == null ? null : p.Cheque.Status,
            p.Remittances.Where(r => r.Remittance.Status != CashRemittanceStatus.CANCELLED).Select(r => new ParcelCheques.Remitted(r.Cheque, r.ConfirmedParcelId)).ToList(), p.StatusAt, p.Code))
        .ToListAsync(cancellationToken);

    private static long PayableOf(BulkRow row) => ParcelCheques.Payable(row.CodAmount, row.DeliveryFee, row.FeeIncluded, row.AlreadyPaid, row.ChequeAmount, row.ChequeStatus, row.Remitted);

    public static async Task<List<CodPayoutBulkCandidateDto>> BulkCandidatesAsync(IAppDbContext db, ICurrentUser caller, string? from, string? to, string? agencyId, CancellationToken cancellationToken)
    {
        var rows = await BulkRowsAsync(BulkWhere(db, caller, from, to, agencyId, null), cancellationToken);
        var groups = new List<CodPayoutBulkCandidateDto>();
        foreach (var row in rows)
        {
            var cash = PayableOf(row);
            if (cash == 0) continue;
            var index = groups.FindIndex(g => g.ClientId == row.ClientId && g.AgencyId == row.AgencyId);
            var cheque = ParcelCheques.PaidOn(row.ChequeAmount, row.ChequeStatus, row.Remitted);
            var current = index < 0 ? new CodPayoutBulkCandidateDto(row.ClientId, row.ClientName, row.AgencyId, row.AgencyName, 0, 0, 0, 0, 0, 0) : groups[index];
            var next = current with
            {
                ParcelCount = current.ParcelCount + 1, Cash = current.Cash + cash, Total = current.Total + cash, ChequeCount = current.ChequeCount + (cheque > 0 ? 1 : 0),
                ChequeAmount = current.ChequeAmount + cheque,
            };
            if (index < 0) groups.Add(next);
            else groups[index] = next;
        }
        var compare = StringComparer.Create(CultureInfo.InvariantCulture, false);
        return groups.OrderBy(g => g.ClientName, compare).ThenBy(g => g.AgencyName, compare).ToList();
    }

    public static async Task<CodPayoutBulkResultDto> CreateBulkAsync(IAppDbContext db, ICurrentUser caller, IReadOnlyList<string> clientIds, string? from, string? to, string? agencyId, string? note,
        CancellationToken cancellationToken)
    {
        var clients = await db.Clients.AsNoTracking().Where(c => clientIds.Contains(c.Id) && c.DeletedAt == null).Select(c => new { c.Id, c.Name, c.BillingType }).ToDictionaryAsync(c => c.Id, cancellationToken);
        var created = new List<CodPayoutBulkCreatedDto>();
        var skipped = new List<CodPayoutBulkSkippedDto>();
        var (prefix, yearly) = await NumberingAsync(db, cancellationToken);
        foreach (var clientId in clientIds)
        {
            if (!clients.TryGetValue(clientId, out var client)) { skipped.Add(new CodPayoutBulkSkippedDto(clientId, clientId, "client-unknown")); continue; }
            if (client.BillingType != BillingType.PROFESSIONAL) { skipped.Add(new CodPayoutBulkSkippedDto(clientId, client.Name, "client-not-professional")); continue; }
            try
            {
                var where = BulkWhere(db, caller, from, to, agencyId, client.Id);
                var reachable = new HashSet<string>();
                foreach (var agency in await where.Select(p => p.AgencyId).Distinct().ToListAsync(cancellationToken))
                {
                    try
                    {
                        await ReturnRows.AssertAgencyReachableAsync(db, caller, agency, cancellationToken);
                        reachable.Add(agency);
                    }
                    catch (BusinessException)
                    {
                        // Hors périmètre : l'agence est écartée, signalée plus bas.
                    }
                }
                var now = DateTime.UtcNow;
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                var lockKey = $"cod-payout-bulk:{client.Id}";
                await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext({lockKey}))", cancellationToken);
                var rows = await BulkRowsAsync(where, cancellationToken);
                var byAgency = new List<(string AgencyId, string Name, List<(string Id, long Amount)> Lines)>();
                foreach (var row in rows)
                {
                    var amount = PayableOf(row);
                    if (amount == 0) continue;
                    var index = byAgency.FindIndex(g => g.AgencyId == row.AgencyId);
                    if (index < 0) byAgency.Add((row.AgencyId, row.AgencyName, [(row.Id, amount)]));
                    else byAgency[index].Lines.Add((row.Id, amount));
                }
                var outOfScope = 0;
                foreach (var group in byAgency)
                {
                    if (!reachable.Contains(group.AgencyId)) { outOfScope++; continue; }
                    var payout = new CodPayout
                    {
                        Code = await DocumentCodes.ReserveAsync(db, prefix, now, cancellationToken, yearly), ClientId = client.Id, AgencyId = group.AgencyId, Note = ReturnRows.Text(note),
                        CreatedById = caller.UserId, CreatedAt = now, UpdatedAt = now,
                    };
                    db.CodPayouts.Add(payout);
                    foreach (var line in group.Lines) db.CodPayoutParcels.Add(new CodPayoutParcel { PayoutId = payout.Id, ParcelId = line.Id, Amount = Money.FromCents(line.Amount) });
                    await db.SaveChangesAsync(cancellationToken);
                    await RefreshTotalAsync(db, payout.Id, cancellationToken);
                    created.Add(new CodPayoutBulkCreatedDto(client.Id, client.Name, group.Name, payout.Code, group.Lines.Count, group.Lines.Sum(l => l.Amount)));
                }
                await transaction.CommitAsync(cancellationToken);
                if (byAgency.Count == 0) skipped.Add(new CodPayoutBulkSkippedDto(clientId, client.Name, "nothing-to-pay"));
                else if (outOfScope > 0) skipped.Add(new CodPayoutBulkSkippedDto(clientId, client.Name, "agency-out-of-scope"));
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                db.ChangeTracker.Clear();
                created.RemoveAll(c => c.ClientId == clientId);
                skipped.Add(new CodPayoutBulkSkippedDto(clientId, client.Name, "error"));
            }
        }
        return new CodPayoutBulkResultDto(created, skipped, created.Sum(c => c.Amount));
    }
}

/// <summary>`codPayoutListQuerySchema`.</summary>
public sealed record CodPayoutListFilter(ListQuery Paging, string? Status, IReadOnlyList<string>? ClientId, IReadOnlyList<string>? AgencyId, string? From, string? To);

internal static class CodPayoutLists
{
    public static IQueryable<CodPayout> Filter(IAppDbContext db, ICurrentUser caller, CodPayoutListFilter filter)
    {
        if (filter.Status is { } s && s is not ("draft" or "paid"))
        {
            throw BusinessException.Validation("Saisie invalide.", new { issues = new[] { new { path = "status", message = Zod.InvalidOption(["draft", "paid"]) } } });
        }
        var where = CodPayouts.Scoped(db.CodPayouts.AsNoTracking(), caller);
        if (caller.ClientId is not null && !caller.IsSuperAdmin()) where = where.Where(p => p.Client.BillingType == BillingType.PROFESSIONAL);
        if (filter.Status is { } status) { var value = status == "draft" ? CodPayoutStatus.DRAFT : CodPayoutStatus.PAID; where = where.Where(p => p.Status == value); }
        if (filter.ClientId is { } clients && caller.MayFilterBy(caller.ClientId)) where = where.Where(p => clients.Contains(p.ClientId));
        if (filter.AgencyId is { } agencies && caller.MayFilterBy(caller.AgencyId)) where = where.Where(p => agencies.Contains(p.AgencyId));
        var (from, to) = ReturnRows.DayRange(filter.From, filter.To);
        if (from is { } f) where = where.Where(p => p.CreatedAt >= f);
        if (to is { } t) where = where.Where(p => p.CreatedAt < t);
        if (filter.Paging.Search is { Length: > 0 } search)
        {
            var needle = search.ToLower();
            where = where.Where(p => p.Code.ToLower().Contains(needle) || (p.Reference != null && p.Reference.ToLower().Contains(needle)) || p.Client.Name.ToLower().Contains(needle));
        }
        return filter.Paging.Order == "asc" ? where.OrderBy(p => p.CreatedAt) : where.OrderByDescending(p => p.CreatedAt);
    }
}

/// <summary>`GET /cod-payouts`.</summary>
public sealed record GetCodPayoutsQuery(CodPayoutListFilter Filter) : IRequest<Paginated<CodPayoutListItemDto>>;

internal sealed class GetCodPayoutsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetCodPayoutsQuery, Paginated<CodPayoutListItemDto>>
{
    public async Task<Paginated<CodPayoutListItemDto>> Handle(GetCodPayoutsQuery request, CancellationToken cancellationToken)
    {
        var where = CodPayoutLists.Filter(db, caller, request.Filter);
        var paging = request.Filter.Paging;
        var total = await where.CountAsync(cancellationToken);
        var items = (await CodPayouts.Project(where.Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize)).ToListAsync(cancellationToken)).Select(CodPayouts.Wire).ToList();
        return new Paginated<CodPayoutListItemDto>(items, total, paging.Page, paging.PageSize);
    }
}

/// <summary>`GET /cod-payouts/export` : classeur « Bons de versement ».</summary>
public sealed record ExportCodPayoutsQuery(CodPayoutListFilter Filter) : IRequest<FileContent>;

internal sealed class ExportCodPayoutsHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportCodPayoutsQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<CodPayoutListItemDto>> Columns =
    [
        new("Bon de versement", r => r.Code),
        new("Statut", r => DocumentLabels.Enum(r.Status)),
        new("E-commerçant", r => r.ClientName),
        new("Agence", r => r.AgencyName),
        new("Colis", r => r.ParcelCount),
        new("Montant versé", r => Exports.Amount(r.Amount)),
        new("Mode", r => DocumentLabels.Enum(r.Method)),
        new("Compte", r => r.BankAccountLabel ?? string.Empty),
        new("Référence", r => r.Reference ?? string.Empty, Text: true),
        new("Versé le", r => Exports.DateTimeText(r.PaidAt)),
        new("Versé par", r => r.PaidByName ?? string.Empty),
        new("Créé le", r => Exports.DateTimeText(r.CreatedAt)),
    ];

    public async Task<FileContent> Handle(ExportCodPayoutsQuery request, CancellationToken cancellationToken)
    {
        var where = CodPayoutLists.Filter(db, caller, request.Filter);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        var rows = (await CodPayouts.Project(where).ToListAsync(cancellationToken)).Select(CodPayouts.Wire).ToList();
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Bons de versement", Columns, rows, cancellationToken);
    }
}

/// <summary>`codPayoutCandidateQuerySchema` : les colis versables d'un vendeur (cash remis, jamais versé, non facturé en régime normal).</summary>
public sealed record GetCodPayoutCandidatesQuery(string? ClientId, string? From, string? To, string? Search) : IRequest<IReadOnlyList<CodPayoutCandidateDto>>;

public sealed class GetCodPayoutCandidatesValidator : ZodValidator<GetCodPayoutCandidatesQuery>
{
    public GetCodPayoutCandidatesValidator()
    {
        RuleFor(x => x.ClientId).Required().MinLength(1, "required");
        RuleFor(x => x.Search).MaxLength(120);
    }
}

internal sealed class GetCodPayoutCandidatesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetCodPayoutCandidatesQuery, IReadOnlyList<CodPayoutCandidateDto>>
{
    public async Task<IReadOnlyList<CodPayoutCandidateDto>> Handle(GetCodPayoutCandidatesQuery request, CancellationToken cancellationToken)
    {
        var where = ParcelFilters.Scoped(db.Parcels.AsNoTracking(), caller).Where(p => p.StatusId == Billing.DeliveredStatusId && p.ReimbursedByCourier
            && !p.CodPayouts.Any(l => l.PaidOutParcelId != null) && !p.InvoiceLines.Any(l => l.InvoicedParcelId != null && l.Invoice.BillingType == BillingType.NORMAL));
        var clientId = caller.MayFilterBy(caller.ClientId) ? request.ClientId : caller.ClientId;
        if (clientId is not null) where = where.Where(p => p.ClientId == clientId);
        var (from, to) = ReturnRows.DayRange(request.From, request.To);
        if (from is { } f) where = where.Where(p => p.ReimbursedAt >= f);
        if (to is { } t) where = where.Where(p => p.ReimbursedAt < t);
        if (!string.IsNullOrEmpty(request.Search))
        {
            var needle = request.Search.ToLower();
            where = where.Where(p => p.Code.ToLower().Contains(needle) || p.RecipientName.ToLower().Contains(needle));
        }
        var rows = await where.OrderBy(p => p.ReimbursedAt).Take(NoteRules.SelectionMax + 1)
            .Select(p => new
            {
                p.Id, p.Code, p.RecipientName, City = p.DestinationCity.Name, p.CodAmount, p.DeliveryFee, p.FeeIncluded, p.AlreadyPaid, p.ReimbursedAt, p.StatusAt,
                ChequeAmount = p.Cheque == null ? (decimal?)null : p.Cheque.Amount, ChequeStatus = p.Cheque == null ? (ChequeStatus?)null : p.Cheque.Status,
                Remitted = p.Remittances.Where(r => r.Remittance.Status != CashRemittanceStatus.CANCELLED).Select(r => new ParcelCheques.Remitted(r.Cheque, r.ConfirmedParcelId)).ToList(),
            }).ToListAsync(cancellationToken);
        return rows.Select(p => new CodPayoutCandidateDto(p.Id, p.Code, p.RecipientName, p.City,
            ParcelCheques.Payable(p.CodAmount, p.DeliveryFee, p.FeeIncluded, p.AlreadyPaid, p.ChequeAmount, p.ChequeStatus, p.Remitted), p.ReimbursedAt, p.StatusAt)).Where(c => c.CodCollected > 0).ToList();
    }
}

/// <summary>`codPayoutBulkQuerySchema` : un bon par vendeur professionnel et par agence, à créer en masse.</summary>
public sealed record GetCodPayoutBulkCandidatesQuery(string? From, string? To, string? AgencyId) : IRequest<IReadOnlyList<CodPayoutBulkCandidateDto>>;

internal sealed class GetCodPayoutBulkCandidatesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetCodPayoutBulkCandidatesQuery, IReadOnlyList<CodPayoutBulkCandidateDto>>
{
    public async Task<IReadOnlyList<CodPayoutBulkCandidateDto>> Handle(GetCodPayoutBulkCandidatesQuery request, CancellationToken cancellationToken) =>
        await CodPayouts.BulkCandidatesAsync(db, caller, request.From, request.To, request.AgencyId, cancellationToken);
}

/// <summary>`codPayoutBulkSchema` : des vendeurs cochés, ou « tous » ; un verrou en base par vendeur.</summary>
public sealed record CreateCodPayoutsInBulkCommand : IRequest<CodPayoutBulkResultDto>
{
    public string? From { get; init => field = Zod.Trim(value); }
    public string? To { get; init => field = Zod.Trim(value); }
    public string? AgencyId { get; init => field = Zod.Trim(value); }
    public bool? All { get; init; }
    public IReadOnlyList<string>? ClientIds { get; init; }
    public string? Note { get; init => field = Zod.Trim(value); }
}

public sealed class CreateCodPayoutsInBulkValidator : ZodValidator<CreateCodPayoutsInBulkCommand>
{
    public CreateCodPayoutsInBulkValidator()
    {
        RuleFor(x => x.ClientIds).Custom((ids, context) =>
        {
            if (ids is null) return;
            if (ids.Count > NoteRules.SelectionMax) context.AddFailure(ValidationPaths.Raw + "clientIds", "too-many");
            for (var i = 0; i < ids.Count; i++) if (string.IsNullOrEmpty(ids[i])) context.AddFailure(ValidationPaths.Raw + $"clientIds.{i}", Zod.TooSmallString(1));
        });
        RuleFor(x => x.Note).MaxLength(500);
    }

    protected override void Refine(CreateCodPayoutsInBulkCommand value, Action<string, string> issue)
    {
        if (value.All != true && (value.ClientIds?.Count ?? 0) == 0) issue(ValidationPaths.Raw + "clientIds", "required");
    }
}

internal sealed class CreateCodPayoutsInBulkHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CreateCodPayoutsInBulkCommand, CodPayoutBulkResultDto>
{
    public async Task<CodPayoutBulkResultDto> Handle(CreateCodPayoutsInBulkCommand input, CancellationToken cancellationToken)
    {
        var clientIds = input.All == true
            ? (await CodPayouts.BulkCandidatesAsync(db, caller, input.From, input.To, input.AgencyId, cancellationToken)).Select(r => r.ClientId).Distinct().ToList()
            : (input.ClientIds ?? []).Distinct().ToList();
        return await CodPayouts.CreateBulkAsync(db, caller, clientIds, input.From, input.To, input.AgencyId, input.Note, cancellationToken);
    }
}

/// <summary>`GET /cod-payouts/:code`.</summary>
public sealed record GetCodPayoutQuery(string Code) : IRequest<CodPayoutDetailDto>;

internal sealed class GetCodPayoutHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetCodPayoutQuery, CodPayoutDetailDto>
{
    public Task<CodPayoutDetailDto> Handle(GetCodPayoutQuery request, CancellationToken cancellationToken) => CodPayouts.DetailAsync(db, caller, request.Code, cancellationToken);
}

/// <summary>`codPayoutCreateSchema` : un brouillon pour un vendeur professionnel.</summary>
public sealed record CreateCodPayoutCommand : IRequest<CodPayoutDetailDto>
{
    public string? ClientId { get; init; }
    public string? AgencyId { get; init; }
    public string? Note { get; init => field = Zod.Trim(value); }
}

public sealed class CreateCodPayoutValidator : ZodValidator<CreateCodPayoutCommand>
{
    public CreateCodPayoutValidator()
    {
        RuleFor(x => x.ClientId).Required().MinLength(1, "required");
        RuleFor(x => x.AgencyId).Required().MinLength(1, "required");
        RuleFor(x => x.Note).MaxLength(500);
    }
}

internal sealed class CreateCodPayoutHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CreateCodPayoutCommand, CodPayoutDetailDto>
{
    public async Task<CodPayoutDetailDto> Handle(CreateCodPayoutCommand input, CancellationToken cancellationToken)
    {
        await ReturnRows.AssertAgencyReachableAsync(db, caller, input.AgencyId!, cancellationToken);
        var client = await db.Clients.AsNoTracking().Where(c => c.Id == input.ClientId && c.DeletedAt == null).Select(c => new { c.Id, c.Name, c.BillingType }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound("E-commerçant introuvable.");
        if (client.BillingType != BillingType.PROFESSIONAL)
        {
            throw BusinessException.Conflict($"{client.Name} n’est pas un e-commerçant professionnel : son COD est réglé par sa facture.", new { reason = "client-not-professional", clientId = client.Id });
        }
        var (prefix, yearly) = await CodPayouts.NumberingAsync(db, cancellationToken);
        var now = DateTime.UtcNow;
        string code;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            code = await DocumentCodes.ReserveAsync(db, prefix, now, cancellationToken, yearly);
            db.CodPayouts.Add(new CodPayout
            {
                Code = code, ClientId = input.ClientId!, AgencyId = input.AgencyId!, Note = ReturnRows.Text(input.Note), CreatedById = caller.UserId, CreatedAt = now, UpdatedAt = now,
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return await CodPayouts.DetailAsync(db, caller, code, cancellationToken);
    }
}

/// <summary>`codPayoutParcelsSchema` : chaque colis livré, cash remis, du même vendeur, jamais versé, avec un montant versable.</summary>
public sealed record AddCodPayoutParcelsCommand : IRequest<CodPayoutDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public IReadOnlyList<string>? ParcelIds { get; init; }
}

public sealed class AddCodPayoutParcelsValidator : ZodValidator<AddCodPayoutParcelsCommand>
{
    public AddCodPayoutParcelsValidator()
    {
        RuleFor(x => x.ParcelIds).Required("array").Custom((ids, context) => RemittanceIds.Check(ids, context, required: true));
    }
}

internal sealed class AddCodPayoutParcelsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<AddCodPayoutParcelsCommand, CodPayoutDetailDto>
{
    public async Task<CodPayoutDetailDto> Handle(AddCodPayoutParcelsCommand input, CancellationToken cancellationToken)
    {
        var payout = await CodPayouts.Scoped(db.CodPayouts.AsNoTracking(), caller).Where(p => p.Code == input.Code).Select(p => new { p.Id, p.Status, p.ClientId }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound($"Bon de versement introuvable : {input.Code}");
        if (payout.Status != CodPayoutStatus.DRAFT) throw BusinessException.Conflict("Ce bon est versé : il ne se modifie plus.", new { reason = "payout-paid" });
        var ids = input.ParcelIds!;
        var parcels = await db.Parcels.AsNoTracking().Where(p => ids.Contains(p.Id) && p.DeletedAt == null)
            .Select(p => new
            {
                p.Id, p.Code, p.ClientId, p.StatusId, p.ReimbursedByCourier, p.CodAmount, p.DeliveryFee, p.FeeIncluded, p.AlreadyPaid,
                PaidOut = p.CodPayouts.Any(l => l.PaidOutParcelId != null),
                NormalInvoice = p.InvoiceLines.Where(l => l.InvoicedParcelId != null && l.Invoice.BillingType == BillingType.NORMAL).Select(l => l.Invoice.Code).FirstOrDefault(),
                ChequeAmount = p.Cheque == null ? (decimal?)null : p.Cheque.Amount, ChequeStatus = p.Cheque == null ? (ChequeStatus?)null : p.Cheque.Status,
                Remitted = p.Remittances.Where(r => r.Remittance.Status != CashRemittanceStatus.CANCELLED).Select(r => new ParcelCheques.Remitted(r.Cheque, r.ConfirmedParcelId)).ToList(),
            }).ToDictionaryAsync(p => p.Id, cancellationToken);
        var amounts = new Dictionary<string, long>();
        foreach (var parcelId in ids)
        {
            var parcel = parcels.GetValueOrDefault(parcelId) ?? throw BusinessException.NotFound($"Colis introuvable : {parcelId}", new { reason = "parcel-unknown", parcelId });
            if (parcel.ClientId != payout.ClientId) throw BusinessException.Conflict($"Le colis {parcel.Code} appartient à un autre e-commerçant.", new { reason = "parcel-other-client", code = parcel.Code });
            if (parcel.StatusId != Billing.DeliveredStatusId)
            {
                throw BusinessException.Conflict($"Le colis {parcel.Code} n'est pas livré.", new { reason = "parcel-not-delivered", code = parcel.Code, statusId = parcel.StatusId });
            }
            if (!parcel.ReimbursedByCourier)
            {
                throw BusinessException.Conflict($"Le cash du colis {parcel.Code} n'a pas encore été remis par le livreur.", new { reason = "parcel-not-reimbursed", code = parcel.Code });
            }
            if (parcel.PaidOut) throw BusinessException.Conflict($"Le COD du colis {parcel.Code} a déjà été versé.", new { reason = "parcel-already-paid-out", code = parcel.Code });
            if (parcel.NormalInvoice is { } invoice) throw CodPayouts.AlreadyInvoiced(parcel.Code, invoice);
            var amount = ParcelCheques.Payable(parcel.CodAmount, parcel.DeliveryFee, parcel.FeeIncluded, parcel.AlreadyPaid, parcel.ChequeAmount, parcel.ChequeStatus, parcel.Remitted);
            if (amount == 0)
            {
                throw BusinessException.Conflict($"Le colis {parcel.Code} est payé par chèque : rien à verser, le chèque est transmis au vendeur.", new { reason = "parcel-paid-by-cheque", code = parcel.Code });
            }
            amounts[parcelId] = amount;
        }
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var (parcelId, amount) in amounts)
        {
            var line = await db.CodPayoutParcels.FirstOrDefaultAsync(l => l.PayoutId == payout.Id && l.ParcelId == parcelId, cancellationToken);
            if (line is null) db.CodPayoutParcels.Add(new CodPayoutParcel { PayoutId = payout.Id, ParcelId = parcelId, Amount = Money.FromCents(amount) });
            else line.Amount = Money.FromCents(amount);
        }
        await db.SaveChangesAsync(cancellationToken);
        await CodPayouts.RefreshTotalAsync(db, payout.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await CodPayouts.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}

/// <summary>`DELETE /cod-payouts/:code/parcels/:parcelId`.</summary>
public sealed record RemoveCodPayoutParcelCommand(string Code, string ParcelId) : IRequest<CodPayoutDetailDto>;

internal sealed class RemoveCodPayoutParcelHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RemoveCodPayoutParcelCommand, CodPayoutDetailDto>
{
    public async Task<CodPayoutDetailDto> Handle(RemoveCodPayoutParcelCommand request, CancellationToken cancellationToken)
    {
        var payout = await CodPayouts.Scoped(db.CodPayouts.AsNoTracking(), caller).Where(p => p.Code == request.Code).Select(p => new { p.Id, p.Status }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound($"Bon de versement introuvable : {request.Code}");
        if (payout.Status != CodPayoutStatus.DRAFT) throw BusinessException.Conflict("Ce bon est versé : il ne se modifie plus.", new { reason = "payout-paid" });
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await db.CodPayoutParcels.Where(l => l.PayoutId == payout.Id && l.ParcelId == request.ParcelId).ExecuteDeleteAsync(cancellationToken);
            await CodPayouts.RefreshTotalAsync(db, payout.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return await CodPayouts.DetailAsync(db, caller, request.Code, cancellationToken);
    }
}

/// <summary>`codPayoutPaySchema` : le versement ; chaque colis est marqué versé, une seule fois.</summary>
public sealed record PayCodPayoutCommand : IRequest<CodPayoutDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? Method { get; init; }
    public string? BankAccountId { get; init => field = Zod.Trim(value); }
    public string? Reference { get; init => field = Zod.Trim(value); }
    public string? PaidAt { get; init => field = Zod.Trim(value); }
    public ProofFileDto? Proof { get; init; }
}

public sealed class PayCodPayoutValidator : ZodValidator<PayCodPayoutCommand>
{
    public PayCodPayoutValidator()
    {
        RuleFor(x => x.Method).Required().OneOf(["cash", "transfer", "check"]);
        RuleFor(x => x.Reference).MaxLength(120);
        RuleFor(x => x.PaidAt).Matches(@"^\d{4}-\d{2}-\d{2}$").WithMessage("invalid-date").When(x => !string.IsNullOrEmpty(x.PaidAt));
        ProofRules.Proof(this, x => x.Proof);
    }
}

internal sealed class PayCodPayoutHandler(IAppDbContext db, ICurrentUser caller, IFileStorage storage) : IRequestHandler<PayCodPayoutCommand, CodPayoutDetailDto>
{
    public async Task<CodPayoutDetailDto> Handle(PayCodPayoutCommand input, CancellationToken cancellationToken)
    {
        var payout = await CodPayouts.Scoped(db.CodPayouts.AsNoTracking(), caller).Where(p => p.Code == input.Code)
                         .Select(p => new
                         {
                             p.Id, p.Status, p.AgencyId,
                             Lines = p.Parcels.Select(l => new
                             {
                                 l.ParcelId, l.Amount, l.Parcel.Code,
                                 NormalInvoice = l.Parcel.InvoiceLines.Where(i => i.InvoicedParcelId != null && i.Invoice.BillingType == BillingType.NORMAL).Select(i => i.Invoice.Code).FirstOrDefault(),
                             }).ToList(),
                         }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound($"Bon de versement introuvable : {input.Code}");
        if (payout.Status == CodPayoutStatus.PAID) throw BusinessException.Conflict("Ce bon de versement est déjà versé.", new { reason = "payout-already-paid" });
        if (payout.Lines.Count == 0) throw BusinessException.Validation("Un bon de versement sans colis ne se verse pas.");
        foreach (var line in payout.Lines) if (line.NormalInvoice is { } invoice) throw CodPayouts.AlreadyInvoiced(line.Code, invoice);
        await ReturnRows.AssertAgencyReachableAsync(db, caller, payout.AgencyId, cancellationToken);
        if (!string.IsNullOrEmpty(input.BankAccountId) && !await db.BankAccounts.AnyAsync(a => a.Id == input.BankAccountId && a.DeletedAt == null, cancellationToken))
        {
            throw BusinessException.NotFound("Compte bancaire introuvable.");
        }
        var now = DateTime.UtcNow;
        var paidAt = string.IsNullOrEmpty(input.PaidAt) ? now : DateTime.SpecifyKind(PettyCashDays.Parse(input.PaidAt).ToDateTime(new TimeOnly(12, 0)), DateTimeKind.Utc);
        var total = payout.Lines.Sum(l => Money.ToCents(l.Amount));
        var proof = input.Proof is null ? null : await storage.PutAsync($"cod-payouts/{payout.Id}", Convert.FromBase64String(input.Proof.Content!), input.Proof.MimeType!, cancellationToken);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            foreach (var line in payout.Lines)
            {
                await db.CodPayoutParcels.Where(l => l.PayoutId == payout.Id && l.ParcelId == line.ParcelId).ExecuteUpdateAsync(s => s.SetProperty(l => l.PaidOutParcelId, line.ParcelId), cancellationToken);
            }
            var row = await db.CodPayouts.FirstAsync(p => p.Id == payout.Id, cancellationToken);
            row.Status = CodPayoutStatus.PAID;
            row.PaidAt = paidAt;
            row.PaidById = caller.UserId;
            row.Amount = Money.FromCents(total);
            row.Method = Enum.Parse<PaymentMethod>(input.Method!.ToUpperInvariant());
            row.BankAccountId = ReturnRows.Text(input.BankAccountId);
            row.Reference = ReturnRows.Text(input.Reference);
            row.UpdatedAt = now;
            if (proof is not null) { row.ProofName = input.Proof!.Name; row.ProofMimeType = input.Proof.MimeType; row.ProofPath = proof.Key; }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception error) when (error is DbUpdateException or Npgsql.PostgresException)
        {
            db.ChangeTracker.Clear();
            if (proof is not null) await storage.RemoveAsync(proof.Key, CancellationToken.None);
            var unique = error is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation } || error.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation };
            if (unique) throw BusinessException.Conflict("Le COD d’un colis de ce bon vient d’être versé ailleurs. Retirez-le et recommencez.", new { reason = "parcel-already-paid-out" });
            throw;
        }
        return await CodPayouts.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}

/// <summary>`GET /cod-payouts/:code/export` : une ligne par colis, montant gelé.</summary>
public sealed record ExportCodPayoutParcelsQuery(string Code) : IRequest<FileContent>;

internal sealed class ExportCodPayoutParcelsHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportCodPayoutParcelsQuery, FileContent>
{
    private sealed record Line(CodPayoutItemDto Item, string? ExternalCode, string RecipientPhone, int StatusId);

    private static readonly IReadOnlyList<ExportColumn<Line>> Columns =
    [
        new("Code", r => r.Item.ParcelCode, Text: true),
        new("Référence commande", r => r.ExternalCode ?? string.Empty, Text: true),
        new("Destinataire", r => r.Item.RecipientName),
        new("Téléphone", r => r.RecipientPhone, Text: true),
        new("Ville", r => r.Item.DestinationCityName),
        new("Statut", r => DocumentLabels.ParcelStatus(r.StatusId)),
        new("Montant versé", r => Exports.Amount(r.Item.Amount)),
    ];

    public async Task<FileContent> Handle(ExportCodPayoutParcelsQuery request, CancellationToken cancellationToken)
    {
        var payout = await CodPayouts.DetailAsync(db, caller, request.Code, cancellationToken);
        var ids = payout.Items.Select(i => i.ParcelId).ToList();
        var extras = await db.Parcels.AsNoTracking().Where(p => ids.Contains(p.Id)).Select(p => new { p.Id, p.ExternalCode, p.RecipientPhone, p.StatusId }).ToDictionaryAsync(p => p.Id, cancellationToken);
        var rows = payout.Items.Select(i => extras.GetValueOrDefault(i.ParcelId) is { } e ? new Line(i, e.ExternalCode, e.RecipientPhone, e.StatusId) : new Line(i, null, string.Empty, 0)).ToList();
        return await Exports.ToXlsxAsync(db, excel, clock, caller, $"Bon de versement {payout.Code}", Columns, rows, cancellationToken);
    }
}

/// <summary>`GET /cod-payouts/:code/pdf` : masqué pour un vendeur ; pied de page réglable.</summary>
public sealed record GetCodPayoutPdfQuery(string Code) : IRequest<FileContent>;

internal sealed class GetCodPayoutPdfHandler(IAppDbContext db, ICurrentUser caller, IPdfService pdf) : IRequestHandler<GetCodPayoutPdfQuery, FileContent>
{
    public async Task<FileContent> Handle(GetCodPayoutPdfQuery request, CancellationToken cancellationToken)
    {
        var p = await CourierMasks.ForReaderAsync(db, caller, await CodPayouts.DetailAsync(db, caller, request.Code, cancellationToken), cancellationToken);
        var company = await PdfCompany.LoadAsync(db, "Bon de versement", cancellationToken);
        var footer = await db.SettingAsync(SettingKeys.CodPayoutFooter, cancellationToken) is { ValueKind: JsonValueKind.String } f ? f.GetString() : null;
        var sheet = new CodPayoutSheet(company, footer, p.Code, p.Status, p.AgencyName, p.ClientName, p.Method, p.BankAccountLabel, p.PaidAt, p.Reference, p.ParcelCount, p.Amount, p.Note,
            p.PaidByName, p.Items.Select(i => new CodPayoutSheetLine(i.ParcelCode, i.RecipientName, i.DestinationCityName, i.Amount)).ToList(),
            p.Cheques.Select(c => new CodPayoutSheetCheque(c.Number, c.Bank, c.ParcelCode, c.Status, c.Amount, c.IncludedInPayout)).ToList());
        return new FileContent(pdf.Render(sheet), FileContent.Pdf, $"{p.Code}.pdf", CacheControl: "private, no-store");
    }
}

/// <summary>`DELETE /cod-payouts/:code` : un bon non versé.</summary>
public sealed record DeleteCodPayoutCommand(string Code) : IRequest<ReturnCodeDto>;

internal sealed class DeleteCodPayoutHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<DeleteCodPayoutCommand, ReturnCodeDto>
{
    public async Task<ReturnCodeDto> Handle(DeleteCodPayoutCommand request, CancellationToken cancellationToken)
    {
        var payout = await CodPayouts.Scoped(db.CodPayouts.AsNoTracking(), caller).Where(p => p.Code == request.Code).Select(p => new { p.Id, p.Status }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound($"Bon de versement introuvable : {request.Code}");
        if (payout.Status == CodPayoutStatus.PAID) throw BusinessException.Conflict("Un bon de versement versé ne se supprime pas.", new { reason = "payout-paid" });
        await db.CodPayouts.Where(p => p.Id == payout.Id).ExecuteDeleteAsync(cancellationToken);
        return new ReturnCodeDto(request.Code);
    }
}
