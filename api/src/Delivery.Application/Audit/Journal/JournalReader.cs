using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Audit.Journal;

/// <summary>`journalQuerySchema` une fois lu.</summary>
public sealed record JournalFilter(int Page, int PageSize, string? View, IReadOnlyList<string>? Category, IReadOnlyList<string>? Action, IReadOnlyList<string>? UserId,
    string? From, string? To, string? Search, string? Entity = null, string? EntityId = null);

/// <summary>Query string brute de `journalQuerySchema`.</summary>
public sealed class JournalParams
{
    public static readonly string[] Views = ["changes", "logins"];
    public static readonly string[] Categories = ["parcels", "notes", "routes", "cash", "invoices", "payouts", "cheques", "returns", "claims", "warehouse", "accounts", "settings", "other"];
    public static readonly string[] Actions = ["create", "update", "delete", "status", "amount", "validate", "issue", "pay", "close", "cancel", "login", "login-failed", "client-change"];
    private static readonly Regex Day = new(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled);

    public string? Page { get; set; }
    public string? PageSize { get; set; }
    public string? View { get; set; }
    public string[]? Category { get; set; }
    public string[]? Action { get; set; }
    public string[]? UserId { get; set; }
    public string? From { get; set; }
    public string? To { get; set; }
    public string? Search { get; set; }

    public JournalFilter ToFilter(string? entity = null, string? entityId = null)
    {
        var paging = new ListQueryParams { Page = Page, PageSize = PageSize, Search = Search }.ToQueryWithDefault(25);
        var issues = new List<object>();
        if (View is not null && !Views.Contains(View)) issues.Add(new { path = "view", message = Zod.InvalidOption(Views) });
        var categories = MultiValue.AsList(Category);
        var actions = MultiValue.AsList(Action);
        for (var i = 0; i < (categories?.Count ?? 0); i++) if (!Categories.Contains(categories![i])) issues.Add(new { path = $"category.{i}", message = Zod.InvalidOption(Categories) });
        for (var i = 0; i < (actions?.Count ?? 0); i++) if (!Actions.Contains(actions![i])) issues.Add(new { path = $"action.{i}", message = Zod.InvalidOption(Actions) });
        if (From is not null && !Day.IsMatch(From.Trim())) issues.Add(new { path = "from", message = "invalid-date" });
        if (To is not null && !Day.IsMatch(To.Trim())) issues.Add(new { path = "to", message = "invalid-date" });
        if (Search is not null && Search.Trim().Length > 120) issues.Add(new { path = "search", message = Zod.TooBigString(120) });
        if (issues.Count > 0) throw BusinessException.Validation("Saisie invalide.", new { issues });
        return new JournalFilter(paging.Page, paging.PageSize, View, categories, actions, MultiValue.AsList(UserId)?.Select(u => u.Trim()).ToList(), From?.Trim(), To?.Trim(), paging.Search, entity, entityId);
    }
}

/// <summary>`JournalService` : les lignes d'audit regroupées par requête, présentées.</summary>
public static class JournalReader
{
    private static readonly Dictionary<string, string[]> NamedStatuses = new()
    {
        ["validate"] = ["VALIDATED"],
        ["issue"] = ["ISSUED"],
        ["pay"] = ["PAID"],
        ["close"] = ["CONFIRMED", "CLOSED", "RESOLVED"],
        ["cancel"] = ["CANCELLED"],
    };

    private sealed class Sql
    {
        public readonly StringBuilder Text = new();
        public readonly List<object> Parameters = [];

        public string P(object value)
        {
            Parameters.Add(value);
            return $"{{{Parameters.Count - 1}}}";
        }
    }

    /// <summary>`where` : la portée, les seules lignes métier, puis les filtres. Sans la reprise des anciens `UPDATE_MANY`.</summary>
    private static async Task<Sql> WhereAsync(IAppDbContext db, ICurrentUser caller, JournalFilter filter, CancellationToken cancellationToken)
    {
        var sql = new Sql();
        var parts = new List<string> { "TRUE" };
        if (caller.ClientId is { } clientId) parts.Add($"a.\"subjectClientId\" = {sql.P(clientId)}");
        parts.Add($"(a.action IN ('LOGIN', 'LOGIN_FAILED') OR (a.entity <> ALL({sql.P(JournalCatalog.Companions.ToArray())}::text[]) AND NOT (a.action = 'UPDATE' AND a.diff <@ {sql.P(JournalCatalog.Technical.ToArray())}::text[])))");
        parts.Add(filter.View == "logins" ? "a.action IN ('LOGIN', 'LOGIN_FAILED')" : "a.action NOT IN ('LOGIN', 'LOGIN_FAILED')");
        if (!string.IsNullOrEmpty(filter.Entity) && !string.IsNullOrEmpty(filter.EntityId)) parts.Add($"a.entity = {sql.P(filter.Entity)} AND a.\"entityId\" = {sql.P(filter.EntityId)}");
        if (filter.Category is { Count: > 0 } categories) parts.Add($"a.entity = ANY({sql.P(categories.SelectMany(JournalCatalog.EntitiesOf).Distinct().ToArray())}::text[])");
        if (filter.Action is { Count: > 0 } actions) parts.Add($"({string.Join(" OR ", actions.Select(a => ActionFilter(sql, a)))})");
        if (filter.UserId is { Count: > 0 } users) parts.Add($"a.\"userId\" = ANY({sql.P(users.ToArray())}::text[])");
        if (filter.From is not null || filter.To is not null)
        {
            var zone = Periods.Zone(await db.Companies.AsNoTracking().OrderBy(c => c.CreatedAt).Select(c => c.Timezone).FirstOrDefaultAsync(cancellationToken));
            if (filter.From is not null) parts.Add($"a.at >= {sql.P(Periods.ZonedDayStart(filter.From, zone))}");
            if (filter.To is not null) parts.Add($"a.at < {sql.P(Periods.ZonedDayStart(Periods.AddDays(filter.To, 1), zone))}");
        }
        if (!string.IsNullOrEmpty(filter.Search))
        {
            var like = $"%{filter.Search.Trim()}%";
            parts.Add($"(a.after->>'code' ILIKE {sql.P(like)} OR a.before->>'code' ILIKE {sql.P(like)} OR a.\"userEmail\" ILIKE {sql.P(like)} OR a.\"requestId\" = {sql.P(filter.Search.Trim())})");
        }
        sql.Text.Append(string.Join(" AND ", parts));
        return sql;
    }

    private static string ActionFilter(Sql sql, string action)
    {
        const string status = "('statusId' = ANY(a.diff) OR 'status' = ANY(a.diff))";
        return action switch
        {
            "create" => "a.action IN ('CREATE', 'CREATE_MANY')",
            "delete" => "(a.action IN ('DELETE', 'DELETE_MANY') OR ('deletedAt' = ANY(a.diff) AND a.after->>'deletedAt' IS NOT NULL))",
            "status" => status,
            "amount" => $"a.diff && {sql.P(JournalCatalog.MoneyFields.ToArray())}::text[]",
            "update" => "a.action IN ('UPDATE', 'UPDATE_MANY')",
            "login" => "a.action = 'LOGIN'",
            "login-failed" => "a.action = 'LOGIN_FAILED'",
            "client-change" => "(a.entity = 'Parcel' AND 'clientId' = ANY(a.diff))",
            _ => $"({status} AND a.after->>'status' = ANY({sql.P(NamedStatuses.GetValueOrDefault(action, []))}::text[]))",
        };
    }

    public static async Task<(List<JournalEntryDetailDto> Items, int Total)> PageAsync(IAppDbContext db, ICurrentUser caller, JournalFilter filter, CancellationToken cancellationToken)
    {
        if (caller.CourierId is not null) return ([], 0);
        var where = await WhereAsync(db, caller, filter, cancellationToken);
        var countSql = $"SELECT count(DISTINCT coalesce(a.\"requestId\", a.id))::int AS \"Value\" FROM audit_logs a WHERE {where.Text}";
        var total = (await db.Database.SqlQueryRaw<int>(countSql, [.. where.Parameters]).ToListAsync(cancellationToken)).FirstOrDefault();
        var pageSql = $"SELECT grp AS \"Value\" FROM (SELECT coalesce(a.\"requestId\", a.id) AS grp, max(a.at) AS last FROM audit_logs a WHERE {where.Text} GROUP BY 1 ORDER BY 2 DESC LIMIT {filter.PageSize} OFFSET {(filter.Page - 1) * filter.PageSize}) g ORDER BY g.last DESC";
        var groups = await db.Database.SqlQueryRaw<string>(pageSql, [.. where.Parameters]).ToListAsync(cancellationToken);
        return (await PresentAsync(db, caller, groups, cancellationToken), total);
    }

    public static async Task<List<JournalEntryDetailDto>> PresentAsync(IAppDbContext db, ICurrentUser caller, IReadOnlyList<string> groupIds, CancellationToken cancellationToken)
    {
        if (groupIds.Count == 0) return [];
        var query = db.AuditLogs.AsNoTracking().Where(a => (a.RequestId != null && groupIds.Contains(a.RequestId)) || (a.RequestId == null && groupIds.Contains(a.Id)));
        if (caller.ClientId is { } clientId) query = query.Where(a => a.SubjectClientId == clientId);
        var stored = await query.OrderBy(a => a.At).ToListAsync(cancellationToken);
        var rows = stored.Select(ToRow).ToList();
        var lookups = await LookupsAsync(db, rows, cancellationToken);
        var byGroup = rows.GroupBy(r => r.RequestId ?? r.Id).ToDictionary(g => g.Key, g => (IReadOnlyList<JournalRow>)g.ToList());
        return groupIds.Select(id => JournalPresenter.PresentGroup(id, byGroup.GetValueOrDefault(id) ?? [], lookups)).OfType<JournalEntryDetailDto>().ToList();
    }

    private static JournalRow ToRow(AuditLog a)
    {
        static JsonElement Json(string? value)
        {
            if (string.IsNullOrEmpty(value)) return default;
            using var document = JsonDocument.Parse(value);
            return document.RootElement.Clone();
        }
        return new JournalRow(a.Id, a.Entity, a.EntityId, a.Action.ToString(), Json(a.Before), Json(a.After), a.Diff, a.UserId, a.UserEmail, a.Role, a.Ip, a.UserAgent, a.RequestId, a.Source, a.At);
    }

    private static async Task<JournalLookups> LookupsAsync(IAppDbContext db, IReadOnlyList<JournalRow> rows, CancellationToken cancellationToken)
    {
        var ids = JournalPresenter.IdsToResolve(rows);
        var cities = ids["cities"].ToList();
        var agencies = ids["agencies"].ToList();
        var couriers = ids["couriers"].ToList();
        var clients = ids["clients"].ToList();
        var users = ids["users"].ToList();
        var parcels = ids["parcels"].ToList();
        return new JournalLookups(
            await db.Cities.AsNoTracking().Where(c => cities.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken),
            await db.Agencies.AsNoTracking().Where(a => agencies.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Name, cancellationToken),
            (await db.Couriers.AsNoTracking().Where(c => couriers.Contains(c.Id)).Select(c => new { c.Id, c.FirstName, c.LastName }).ToListAsync(cancellationToken))
                .ToDictionary(c => c.Id, c => $"{c.FirstName} {c.LastName}".Trim()),
            await db.Clients.AsNoTracking().Where(c => clients.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken),
            (await db.Users.AsNoTracking().Where(u => users.Contains(u.Id)).Select(u => new { u.Id, u.FirstName, u.LastName, u.Email, u.Username }).ToListAsync(cancellationToken))
                .ToDictionary(u => u.Id, u =>
                {
                    var name = $"{u.FirstName} {u.LastName}".Trim();
                    return (name.Length > 0 ? name : u.Email ?? u.Username ?? string.Empty, u.Email);
                }),
            await db.Parcels.AsNoTracking().Where(p => parcels.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Code, cancellationToken),
            await ReasonsAsync(db, rows, cancellationToken));
    }

    /// <summary>`reasons` : le motif saisi à ±15 s d'une modification.</summary>
    private static async Task<Dictionary<string, string>> ReasonsAsync(IAppDbContext db, IReadOnlyList<JournalRow> rows, CancellationToken cancellationToken)
    {
        var reasons = new Dictionary<string, string>();
        var touched = rows.Where(r => r.Action == "UPDATE" && r.EntityId.Length > 0).ToList();
        if (touched.Count == 0) return reasons;
        var from = touched.Min(r => r.At).AddSeconds(-15);
        var to = touched.Max(r => r.At).AddSeconds(15);
        var ids = touched.Select(r => r.EntityId).Distinct().ToList();
        var changes = await db.FieldChanges.AsNoTracking().Where(c => ids.Contains(c.EntityId) && c.At >= from && c.At <= to && c.Reason != null)
            .Select(c => new { c.EntityId, c.Reason, c.At }).ToListAsync(cancellationToken);
        var fees = await db.ParcelFeeChanges.AsNoTracking().Where(f => ids.Contains(f.ParcelId) && f.CreatedAt >= from && f.CreatedAt <= to)
            .Select(f => new { f.ParcelId, f.Reason, f.CreatedAt }).ToListAsync(cancellationToken);
        bool Near(string id, DateTime at) => touched.Any(r => r.EntityId == id && Math.Abs((r.At - at).TotalMilliseconds) <= 15_000);
        foreach (var change in changes) if (!string.IsNullOrEmpty(change.Reason) && Near(change.EntityId, change.At)) reasons[change.EntityId] = change.Reason;
        foreach (var fee in fees) if (!string.IsNullOrEmpty(fee.Reason) && Near(fee.ParcelId, fee.CreatedAt)) reasons[fee.ParcelId] = fee.Reason;
        return reasons;
    }
}
