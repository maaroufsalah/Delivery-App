using System.Text.Json;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Audit;

/// <summary>`AuditLogListItem`.</summary>
public class AuditLogListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public List<string> Diff { get; set; } = [];
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? Role { get; set; }
    public string? Ip { get; set; }
    public DateTime At { get; set; }
}

/// <summary>`AuditLogDetail` : avec l'état avant et après.</summary>
public sealed class AuditLogDetailDto : AuditLogListItemDto
{
    [System.Text.Json.Serialization.JsonPropertyOrder(1)]
    public JsonElement? Before { get; set; }

    [System.Text.Json.Serialization.JsonPropertyOrder(1)]
    public JsonElement? After { get; set; }

    [System.Text.Json.Serialization.JsonPropertyOrder(1)]
    public string? UserAgent { get; set; }

    [System.Text.Json.Serialization.JsonPropertyOrder(1)]
    public string? RequestId { get; set; }
}

/// <summary>`auditListQuerySchema` une fois lu.</summary>
public sealed record AuditListFilter(int Page, int PageSize, string? Search, string? Entity, string? EntityId, string? Action, string? UserId, string? From, string? To);

/// <summary>Query string brute de `auditListQuerySchema`.</summary>
public sealed class AuditListParams
{
    public string? Page { get; set; }
    public string? PageSize { get; set; }
    public string? Search { get; set; }
    public string? Entity { get; set; }
    public string? EntityId { get; set; }
    public string? Action { get; set; }
    public string? UserId { get; set; }
    public string? From { get; set; }
    public string? To { get; set; }

    public AuditListFilter ToFilter(string? entity = null, string? entityId = null)
    {
        var paging = new ListQueryParams { Page = Page, PageSize = PageSize, Search = Search }.ToQueryWithDefault(25);
        var actions = Enum.GetValues<AuditAction>().Select(Money.ToWire).ToList();
        var issues = new List<object>();
        if (Action is not null && !actions.Contains(Action)) issues.Add(new { path = "action", message = Common.Validation.Zod.InvalidOption(actions) });
        foreach (var (name, value, max) in new[] { ("entity", Entity, 60), ("entityId", EntityId, 60) })
        {
            if (value is not null && value.Trim().Length > max) issues.Add(new { path = name, message = Common.Validation.Zod.TooBigString(max) });
        }
        if (issues.Count > 0) throw BusinessException.Validation("Saisie invalide.", new { issues });
        return new AuditListFilter(paging.Page, paging.PageSize, paging.Search, entity ?? Entity?.Trim(), entityId ?? EntityId?.Trim(), Action, UserId?.Trim(), From?.Trim(), To?.Trim());
    }
}

public static class AuditRows
{
    /// <summary>`auditWhere` : le vendeur ne voit que ce qui le concerne, le livreur rien.</summary>
    public static IQueryable<AuditLog> Scoped(IQueryable<AuditLog> logs, ICurrentUser caller)
    {
        if (caller.IsSuperAdmin()) return logs;
        if (caller.ClientId is { } clientId) return logs.Where(l => l.SubjectClientId == clientId);
        if (caller.CourierId is not null) return logs.Where(l => l.Id == Scope.DenyAllId);
        return logs;
    }

    public static async Task<IQueryable<AuditLog>> FilterAsync(IAppDbContext db, ICurrentUser caller, AuditListFilter filter, CancellationToken cancellationToken)
    {
        var where = Scoped(db.AuditLogs.AsNoTracking(), caller);
        if (!string.IsNullOrEmpty(filter.Entity)) where = where.Where(l => l.Entity == filter.Entity);
        if (!string.IsNullOrEmpty(filter.EntityId)) where = where.Where(l => l.EntityId == filter.EntityId);
        if (!string.IsNullOrEmpty(filter.UserId)) where = where.Where(l => l.UserId == filter.UserId);
        if (!string.IsNullOrEmpty(filter.Action))
        {
            var action = Enum.Parse<AuditAction>(Money.FromWire(filter.Action));
            where = where.Where(l => l.Action == action);
        }
        if (!string.IsNullOrEmpty(filter.From) || !string.IsNullOrEmpty(filter.To))
        {
            var zone = Periods.Zone(await db.Companies.AsNoTracking().OrderBy(c => c.CreatedAt).Select(c => c.Timezone).FirstOrDefaultAsync(cancellationToken));
            // Un jour illisible fait échouer Prisma (`Invalid Date`, 500) : reproduit par l'exception de lecture.
            if (!string.IsNullOrEmpty(filter.From)) { var start = Periods.ZonedDayStart(filter.From, zone); where = where.Where(l => l.At >= start); }
            if (!string.IsNullOrEmpty(filter.To)) { var end = Periods.ZonedDayStart(Periods.AddDays(filter.To, 1), zone); where = where.Where(l => l.At < end); }
        }
        if (!string.IsNullOrEmpty(filter.Search))
        {
            var search = filter.Search;
            var pattern = Like.Contains(search);
            where = where.Where(l => l.EntityId == search || EF.Functions.ILike(l.UserEmail!, pattern) || l.RequestId == search);
        }
        return where.OrderByDescending(l => l.At);
    }

    public static T Fill<T>(T dto, AuditLog row)
        where T : AuditLogListItemDto
    {
        dto.Id = row.Id;
        dto.Entity = row.Entity;
        dto.EntityId = row.EntityId;
        dto.Action = Money.ToWire(row.Action);
        dto.Diff = row.Diff;
        dto.UserId = row.UserId;
        dto.UserEmail = row.UserEmail;
        dto.Role = row.Role;
        dto.Ip = row.Ip;
        dto.At = row.At;
        return dto;
    }

    public static JsonElement? Json(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        using var document = JsonDocument.Parse(value);
        return document.RootElement.ValueKind == JsonValueKind.Null ? null : document.RootElement.Clone();
    }
}
