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
