using System.Globalization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.PettyCash;

public sealed record PettyCashDaySummaryDto(string Day, string? AgencyId, string? AgencyName, int Agencies, long OpeningBalance, long Supplied, long Spent, long ClosingBalance, int PendingDrafts,
    string Status, DateTime? ClosedAt, string? ClosedBy, DateTime? ReopenedAt, string? ReopenedByName, string? ReopenReason);

public sealed record PettyCashDaysPageDto(IReadOnlyList<PettyCashDaySummaryDto> Items, string Today, PettyCashDaySummaryDto? Current);

public sealed record PettyCashDayDetailDto(PettyCashDaySummaryDto Summary, IReadOnlyList<PettyCashDaySummaryDto> Agencies);

/// <summary>
/// `PettyCashDaysService` : une journée de caisse par agence, son solde chaîné d'un jour à l'autre, sa clôture.
/// Toute écriture passe par <see cref="WriteAsync{T}"/> : verrou en base par agence, refus d'une journée close,
/// puis lignes de journée et soldes recalculés dans la même transaction.
/// </summary>
public static class PettyCashDays
{
    public static string DayText(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string DayOf(DateTime occurredAt) => occurredAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static DateOnly Parse(string day) => DateOnly.ParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static BusinessException ClosedDay(string day) => BusinessException.Conflict(
        $"La journée de caisse du {string.Join('/', day.Split('-').Reverse())} est clôturée : rouvrez-la (« Corriger ») pour la modifier.",
        new { reason = "petty-cash-day-closed", day });

    public static Task LockAsync(IAppDbContext db, string agencyId, CancellationToken cancellationToken)
    {
        var key = $"petty-cash-day:{agencyId}";
        return db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))", cancellationToken);
    }

    public static async Task<T> WriteAsync<T>(IAppDbContext db, IClock clock, IReadOnlyList<(string AgencyId, string Day)> targets, Func<Task<T>> write, CancellationToken cancellationToken,
        bool draftOnly = false)
    {
        var today = clock.Today();
        var agencies = targets.Select(t => t.AgencyId).Distinct().Order(StringComparer.Ordinal).ToList();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var agencyId in agencies) await LockAsync(db, agencyId, cancellationToken);
        if (!draftOnly)
        {
            foreach (var (agencyId, day) in targets) await AssertWritableAsync(db, agencyId, day, today, cancellationToken);
        }
        var result = await write();
        foreach (var agencyId in agencies)
        {
            await SyncRowsAsync(db, agencyId, today, PettyCashDayStatus.OPEN, clock.UtcNow, cancellationToken);
            await RecomputeAsync(db, agencyId, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static async Task AssertWritableAsync(IAppDbContext db, string agencyId, string day, string today, CancellationToken cancellationToken)
    {
        var date = Parse(day);
        var row = await db.PettyCashDays.AsNoTracking().Where(d => d.AgencyId == agencyId && d.Day == date).Select(d => new { d.Status }).FirstOrDefaultAsync(cancellationToken);
        if (row?.Status == PettyCashDayStatus.CLOSED) throw ClosedDay(day);
        if (row is not null || string.CompareOrdinal(day, today) >= 0) return;
        if (await db.PettyCashDays.AnyAsync(d => d.AgencyId == agencyId && d.Status == PettyCashDayStatus.CLOSED && d.Day > date, cancellationToken)) throw ClosedDay(day);
    }

    /// <summary>`syncRows` : une ligne par jour portant un mouvement ; les jours vidés disparaissent.</summary>
    private static async Task<(int Created, int Removed)> SyncRowsAsync(IAppDbContext db, string agencyId, string today, PettyCashDayStatus pastStatus, DateTime now,
        CancellationToken cancellationToken)
    {
        var movementDays = (await db.PettyCashMovements.AsNoTracking().Where(m => m.AgencyId == agencyId).Select(m => m.OccurredAt).Distinct().ToListAsync(cancellationToken))
            .Select(DayOf).ToHashSet();
        var rows = await db.PettyCashDays.AsNoTracking().Where(d => d.AgencyId == agencyId).Select(d => new { d.Id, d.Day }).ToListAsync(cancellationToken);
        var existing = rows.Select(r => DayText(r.Day)).ToHashSet();
        var created = 0;
        foreach (var day in movementDays.Where(d => !existing.Contains(d)).Order(StringComparer.Ordinal))
        {
            var closed = string.CompareOrdinal(day, today) < 0 && pastStatus == PettyCashDayStatus.CLOSED;
            db.PettyCashDays.Add(new PettyCashDay
            {
                AgencyId = agencyId, Day = Parse(day), Status = closed ? PettyCashDayStatus.CLOSED : PettyCashDayStatus.OPEN, ClosedAt = closed ? now : null, ClosedAutomatically = closed,
                CreatedAt = now, UpdatedAt = now,
            });
            created++;
        }
        if (created > 0) await db.SaveChangesAsync(cancellationToken);
        var empty = rows.Where(r => !movementDays.Contains(DayText(r.Day))).Select(r => r.Id).ToList();
        var removed = empty.Count == 0 ? 0 : await db.PettyCashDays.Where(d => empty.Contains(d.Id)).ExecuteDeleteAsync(cancellationToken);
        return (created, removed);
    }

    private sealed record Chain(string Id, string Day, long Opening, long Supplied, long Spent, long Closing, int Drafts);

    /// <summary>`recompute` / `computeChain` : soldes d'ouverture et de clôture chaînés, dépenses validées seulement.</summary>
    public static async Task RecomputeAsync(IAppDbContext db, string agencyId, CancellationToken cancellationToken)
    {
        var sums = await db.PettyCashMovements.AsNoTracking().Where(m => m.AgencyId == agencyId).GroupBy(m => new { m.OccurredAt, m.Kind, m.Status })
            .Select(g => new { g.Key.OccurredAt, g.Key.Kind, g.Key.Status, Sum = g.Sum(m => m.Amount), Count = g.Count() }).ToListAsync(cancellationToken);
        var rows = await db.PettyCashDays.AsNoTracking().Where(d => d.AgencyId == agencyId).OrderBy(d => d.Day)
            .Select(d => new { d.Id, d.Day, d.OpeningBalance, d.Supplied, d.Spent, d.ClosingBalance, d.PendingDrafts }).ToListAsync(cancellationToken);
        var perDay = new Dictionary<string, (long Supplied, long Spent, int Drafts)>();
        foreach (var sum in sums)
        {
            var day = DayOf(sum.OccurredAt);
            var entry = perDay.GetValueOrDefault(day);
            var cents = Money.ToCents(sum.Sum);
            if (sum.Status == PettyCashStatus.VALIDATED && sum.Kind == PettyCashKind.SUPPLY) entry.Supplied += cents;
            if (sum.Status == PettyCashStatus.VALIDATED && sum.Kind == PettyCashKind.EXPENSE) entry.Spent += cents;
            if (sum.Status == PettyCashStatus.DRAFT) entry.Drafts += sum.Count;
            perDay[day] = entry;
        }
        long balance = 0;
        if (rows.Count > 0)
        {
            var first = DayText(rows[0].Day);
            foreach (var (day, entry) in perDay) if (string.CompareOrdinal(day, first) < 0) balance += entry.Supplied - entry.Spent;
        }
        foreach (var row in rows)
        {
            var entry = perDay.GetValueOrDefault(DayText(row.Day));
            var next = new Chain(row.Id, DayText(row.Day), balance, entry.Supplied, entry.Spent, balance + entry.Supplied - entry.Spent, entry.Drafts);
            balance = next.Closing;
            if (next.Opening == Money.ToCents(row.OpeningBalance) && next.Supplied == Money.ToCents(row.Supplied) && next.Spent == Money.ToCents(row.Spent)
                && next.Closing == Money.ToCents(row.ClosingBalance) && next.Drafts == row.PendingDrafts) continue;
            await db.PettyCashDays.Where(d => d.Id == next.Id).ExecuteUpdateAsync(set => set.SetProperty(d => d.OpeningBalance, Money.FromCents(next.Opening))
                .SetProperty(d => d.Supplied, Money.FromCents(next.Supplied)).SetProperty(d => d.Spent, Money.FromCents(next.Spent))
                .SetProperty(d => d.ClosingBalance, Money.FromCents(next.Closing)).SetProperty(d => d.PendingDrafts, next.Drafts).SetProperty(d => d.UpdatedAt, DateTime.UtcNow), cancellationToken);
        }
    }

    /// <summary>`courierDay` : la dépense d'un livreur tombe aujourd'hui, ou demain si la journée est déjà close.</summary>
    public static async Task<string> CourierDayAsync(IAppDbContext db, IClock clock, string agencyId, CancellationToken cancellationToken)
    {
        var today = clock.Today();
        var date = Parse(today);
        var closed = await db.PettyCashDays.AnyAsync(d => d.AgencyId == agencyId && d.Day == date && d.Status == PettyCashDayStatus.CLOSED, cancellationToken);
        return closed ? Periods.AddDays(today, 1) : today;
    }

    public static async Task<Dictionary<string, string>> StatusesAsync(IAppDbContext db, IReadOnlyList<(string AgencyId, string Day)> keys, CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, string>();
        if (keys.Count == 0) return map;
        var agencies = keys.Select(k => k.AgencyId).Distinct().ToList();
        var days = keys.Select(k => Parse(k.Day)).Distinct().ToList();
        var rows = await db.PettyCashDays.AsNoTracking().Where(d => agencies.Contains(d.AgencyId) && days.Contains(d.Day)).Select(d => new { d.AgencyId, d.Day, d.Status })
            .ToListAsync(cancellationToken);
        foreach (var row in rows) map[$"{row.AgencyId}|{DayText(row.Day)}"] = row.Status == PettyCashDayStatus.CLOSED ? "closed" : "open";
        return map;
    }

    /// <summary>`scopeWhere` : administrateur tout (ou l'agence demandée), agent son agence, les autres rien.</summary>
    public static IQueryable<PettyCashDay> Scoped(IQueryable<PettyCashDay> days, ICurrentUser caller, string? agencyId)
    {
        if (caller.IsSuperAdmin()) return string.IsNullOrEmpty(agencyId) ? days : days.Where(d => d.AgencyId == agencyId);
        if (caller.ClientId is not null || caller.CourierId is not null || caller.AgencyId is null) return days.Where(d => d.Id == Scope.DenyAllId);
        if (!string.IsNullOrEmpty(agencyId) && agencyId != caller.AgencyId) return days.Where(d => d.Id == Scope.DenyAllId);
        var own = caller.AgencyId;
        return days.Where(d => d.AgencyId == own);
    }

    private sealed record DayRow(string Id, string AgencyId, string AgencyName, DateOnly Day, decimal OpeningBalance, decimal Supplied, decimal Spent, decimal ClosingBalance, int PendingDrafts,
        PettyCashDayStatus Status, DateTime? ClosedAt, bool ClosedAutomatically, string? ClosedByName, DateTime? ReopenedAt, string? ReopenedByName, string? ReopenReason);

    private static IQueryable<DayRow> Rows(IQueryable<PettyCashDay> days) => days.Select(d => new DayRow(d.Id, d.AgencyId, d.Agency.Name, d.Day, d.OpeningBalance, d.Supplied, d.Spent,
        d.ClosingBalance, d.PendingDrafts, d.Status, d.ClosedAt, d.ClosedAutomatically, d.ClosedBy != null ? d.ClosedBy.FirstName + " " + d.ClosedBy.LastName : null, d.ReopenedAt,
        d.ReopenedBy != null ? d.ReopenedBy.FirstName + " " + d.ReopenedBy.LastName : null, d.ReopenReason));

    /// <summary>`carriedBalances` : le solde des agences sans journée ce jour-là, reporté de leur dernière clôture.</summary>
    private static Dictionary<string, long> Carried(IEnumerable<(string AgencyId, DateOnly Day, decimal Closing)> rows)
    {
        var byDay = rows.GroupBy(r => DayText(r.Day)).ToDictionary(g => g.Key, g => g.ToList());
        var last = new Dictionary<string, long>();
        var carried = new Dictionary<string, long>();
        foreach (var day in byDay.Keys.Order(StringComparer.Ordinal))
        {
            var present = byDay[day].Select(e => e.AgencyId).ToHashSet();
            carried[day] = last.Where(kv => !present.Contains(kv.Key)).Sum(kv => kv.Value);
            foreach (var entry in byDay[day]) last[entry.AgencyId] = Money.ToCents(entry.Closing);
        }
        return carried;
    }

    private static PettyCashDaySummaryDto Aggregate(IReadOnlyList<DayRow> rows, ICurrentUser caller, string? agencyId, long carried = 0)
    {
        var single = rows.Count == 1 ? rows[0] : null;
        var open = rows.Any(r => r.Status == PettyCashDayStatus.OPEN);
        var closedRows = rows.Where(r => r.Status == PettyCashDayStatus.CLOSED && r.ClosedAt is not null).ToList();
        var lastClosed = closedRows.OrderByDescending(r => r.ClosedAt).FirstOrDefault();
        var lastManual = closedRows.Where(r => !r.ClosedAutomatically).OrderByDescending(r => r.ClosedAt).FirstOrDefault();
        var lastReopened = rows.Where(r => r.ReopenedAt is not null).OrderByDescending(r => r.ReopenedAt).FirstOrDefault();
        var showAgency = single is not null && (!string.IsNullOrEmpty(agencyId) || !caller.IsSuperAdmin() || rows.Count == 1);
        return new PettyCashDaySummaryDto(DayText(rows[0].Day), showAgency ? single!.AgencyId : null, showAgency ? single!.AgencyName : null, rows.Count,
            carried + rows.Sum(r => Money.ToCents(r.OpeningBalance)), rows.Sum(r => Money.ToCents(r.Supplied)), rows.Sum(r => Money.ToCents(r.Spent)),
            carried + rows.Sum(r => Money.ToCents(r.ClosingBalance)), rows.Sum(r => r.PendingDrafts), open ? "open" : "closed",
            !open && lastClosed?.ClosedAt is { } at ? at : null,
            open ? null : lastManual is not null ? lastManual.ClosedByName : closedRows.Count > 0 ? "automatic" : null,
            lastReopened?.ReopenedAt, lastReopened?.ReopenedByName, lastReopened?.ReopenReason);
    }

    public static async Task<PettyCashDaysPageDto> ListAsync(IAppDbContext db, ICurrentUser caller, IClock clock, string? agencyId, string? status, bool withDrafts, int limit,
        CancellationToken cancellationToken)
    {
        var today = clock.Today();
        var rows = await Rows(Scoped(db.PettyCashDays.AsNoTracking(), caller, agencyId).OrderByDescending(d => d.Day).ThenBy(d => d.AgencyId)).ToListAsync(cancellationToken);
        var carried = Carried(rows.Select(r => (r.AgencyId, r.Day, r.ClosingBalance)));
        var days = rows.GroupBy(r => r.Day).Select(g => Aggregate(g.ToList(), caller, agencyId, carried.GetValueOrDefault(DayText(g.Key)))).ToList();
        var current = days.FirstOrDefault(d => d.Day == today);
        var items = days.Where(d => status is null || d.Status == status).Where(d => !withDrafts || d.PendingDrafts > 0).Take(limit).ToList();
        return new PettyCashDaysPageDto(items, today, current);
    }

    private static async Task<List<DayRow>> DayRowsAsync(IAppDbContext db, ICurrentUser caller, string day, string? agencyId, CancellationToken cancellationToken)
    {
        var date = Parse(day);
        var rows = await Rows(Scoped(db.PettyCashDays.AsNoTracking(), caller, agencyId).Where(d => d.Day == date).OrderBy(d => d.AgencyId)).ToListAsync(cancellationToken);
        if (rows.Count == 0) throw BusinessException.NotFound("Aucune journée de caisse à cette date.", new { reason = "petty-cash-day-not-found" });
        return rows;
    }

    public static async Task<PettyCashDayDetailDto> DetailAsync(IAppDbContext db, ICurrentUser caller, string day, string? agencyId, CancellationToken cancellationToken)
    {
        var rows = await DayRowsAsync(db, caller, day, agencyId, cancellationToken);
        var date = Parse(day);
        var upTo = await Scoped(db.PettyCashDays.AsNoTracking(), caller, agencyId).Where(d => d.Day <= date).OrderBy(d => d.Day)
            .Select(d => new { d.AgencyId, d.Day, d.ClosingBalance }).ToListAsync(cancellationToken);
        var carried = Carried(upTo.Select(r => (r.AgencyId, r.Day, r.ClosingBalance)));
        return new PettyCashDayDetailDto(Aggregate(rows, caller, agencyId, carried.GetValueOrDefault(day)), rows.Select(r => Aggregate([r], caller, r.AgencyId)).ToList());
    }

    public static async Task<PettyCashDayDetailDto> CloseAsync(IAppDbContext db, ICurrentUser caller, string day, string? agencyId, CancellationToken cancellationToken)
    {
        var rows = await DayRowsAsync(db, caller, day, agencyId, cancellationToken);
        var open = rows.Where(r => r.Status == PettyCashDayStatus.OPEN).ToList();
        if (open.Count > 0)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            foreach (var agency in open.Select(r => r.AgencyId).Distinct().Order(StringComparer.Ordinal))
            {
                await LockAsync(db, agency, cancellationToken);
                await RecomputeAsync(db, agency, cancellationToken);
            }
            var userId = caller.UserId;
            foreach (var row in open)
            {
                await db.PettyCashDays.Where(d => d.Id == row.Id).ExecuteUpdateAsync(set => set.SetProperty(d => d.Status, PettyCashDayStatus.CLOSED).SetProperty(d => d.ClosedAt, DateTime.UtcNow)
                    .SetProperty(d => d.ClosedById, userId).SetProperty(d => d.ClosedAutomatically, false), cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        return await DetailAsync(db, caller, day, agencyId, cancellationToken);
    }

    public static async Task<PettyCashDayDetailDto> ReopenAsync(IAppDbContext db, ICurrentUser caller, string day, string? agencyId, string reason, CancellationToken cancellationToken)
    {
        var rows = await DayRowsAsync(db, caller, day, agencyId, cancellationToken);
        var closed = rows.Where(r => r.Status == PettyCashDayStatus.CLOSED).ToList();
        if (closed.Count > 0)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            foreach (var agency in closed.Select(r => r.AgencyId).Distinct().Order(StringComparer.Ordinal)) await LockAsync(db, agency, cancellationToken);
            var userId = caller.UserId;
            foreach (var row in closed)
            {
                await db.PettyCashDays.Where(d => d.Id == row.Id).ExecuteUpdateAsync(set => set.SetProperty(d => d.Status, PettyCashDayStatus.OPEN).SetProperty(d => d.ClosedAt, (DateTime?)null)
                    .SetProperty(d => d.ClosedById, (string?)null).SetProperty(d => d.ClosedAutomatically, false).SetProperty(d => d.ReopenedAt, DateTime.UtcNow)
                    .SetProperty(d => d.ReopenedById, userId).SetProperty(d => d.ReopenReason, reason).SetProperty(d => d.ReopenCount, d => d.ReopenCount + 1), cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        return await DetailAsync(db, caller, day, agencyId, cancellationToken);
    }

    /// <summary>`sweepCompany` : chaque minute, les journées passées encore ouvertes se closent seules (sauf rouvertes aujourd'hui).</summary>
    public static async Task<(int Created, int Closed)> SweepAsync(IAppDbContext db, IClock clock, CancellationToken cancellationToken)
    {
        var today = clock.Today();
        var now = clock.UtcNow;
        var agencies = (await db.PettyCashMovements.AsNoTracking().Select(m => m.AgencyId).Distinct().ToListAsync(cancellationToken))
            .Union(await db.PettyCashDays.AsNoTracking().Select(d => d.AgencyId).Distinct().ToListAsync(cancellationToken)).Order(StringComparer.Ordinal).ToList();
        int created = 0, closed = 0;
        var todayDate = Parse(today);
        foreach (var agencyId in agencies)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await LockAsync(db, agencyId, cancellationToken);
            created += (await SyncRowsAsync(db, agencyId, today, PettyCashDayStatus.CLOSED, now, cancellationToken)).Created;
            await RecomputeAsync(db, agencyId, cancellationToken);
            var open = await db.PettyCashDays.AsNoTracking().Where(d => d.AgencyId == agencyId && d.Status == PettyCashDayStatus.OPEN && d.Day < todayDate).OrderBy(d => d.Day)
                .Select(d => new { d.Id, d.ReopenedAt }).ToListAsync(cancellationToken);
            foreach (var row in open)
            {
                if (row.ReopenedAt is { } reopened && string.CompareOrdinal(Periods.LocalDay(reopened, ClockDays.Zone), today) >= 0) continue;
                await db.PettyCashDays.Where(d => d.Id == row.Id).ExecuteUpdateAsync(set => set.SetProperty(d => d.Status, PettyCashDayStatus.CLOSED).SetProperty(d => d.ClosedAt, now)
                    .SetProperty(d => d.ClosedById, (string?)null).SetProperty(d => d.ClosedAutomatically, true), cancellationToken);
                closed++;
            }
            await transaction.CommitAsync(cancellationToken);
        }
        return (created, closed);
    }
}
